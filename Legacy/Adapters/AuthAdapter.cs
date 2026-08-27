using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using TOPqwWeb_Core.Legacy.Bridge;

namespace TOPqwWeb_Core.Legacy.Adapters
{
    public class AuthAdapter
    {
        private const string InternalDecryptionKey = "HwöIM4Cü)Öy5Fd}D),fC6_T?";
        private readonly string _connectionString;

        public AuthAdapter(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TOPqwWeb")
                ?? throw new InvalidOperationException("ConnectionStrings:TOPqwWeb muss konfiguriert werden.");
        }

        public async Task<AuthResponse?> LoginEncAsync(string szName, string szPW, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var useSaltedHash = await GetSaltedHashOptionAsync(connection, cancellationToken);
            if (!useSaltedHash)
            {
                return await LoginLegacyAsync(connection, szName, szPW, cancellationToken);
            }

            var symKey = LegacyCrypto.AesEncode(szPW);

            await using var command = new SqlCommand("[dbo].[LoginUser]", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@username", szName);
            command.Parameters.AddWithValue("@pw", szPW);
            command.Parameters.AddWithValue("@symKey", symKey);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            var loginStatus = value is null || value == DBNull.Value ? -1 : Convert.ToInt32(value);
            if (loginStatus <= 0) return new AuthResponse { UserId = loginStatus, LoginStatus = loginStatus };
            var userId = loginStatus;

            await using var userCommand = new SqlCommand(
                "SELECT BEID, Benutzername, Email, ISNULL(IsDeactivated, 0), ISNULL(Benutzertyp, 9), ISNULL(Admin, 0), ISNULL(Kosoz, 0) FROM [Benutzer] WHERE BEID = @beid",
                connection);
            userCommand.Parameters.AddWithValue("@beid", userId);
            await using var reader = await userCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return new AuthResponse { UserId = userId, LoginStatus = loginStatus };

            var username = Convert.ToString(reader.GetValue(1)) ?? szName;
            var email = reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2));
            var isDeactivated = Convert.ToBoolean(reader.GetValue(3));
            var userType = Convert.ToInt32(reader.GetValue(4));
            var isAdmin = Convert.ToBoolean(reader.GetValue(5));
            var isKosoz = Convert.ToBoolean(reader.GetValue(6));
            await reader.CloseAsync();
            var modules = (await GetUserModulesAsync(connection, userId, cancellationToken)).ToArray();

            return new AuthResponse
            {
                UserId = userId,
                LoginStatus = loginStatus,
                Username = username,
                Email = email,
                IsDeactivated = isDeactivated,
                UserType = userType,
                IsAdmin = isAdmin,
                IsKosoz = isKosoz,
                Modules = modules
            };
        }

        private async Task<IReadOnlyList<int>> GetUserModulesAsync(SqlConnection connection, int BEID, CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand("SELECT DISTINCT MID FROM Benutzer be LEFT JOIN IndexBenutzerGruppe ibg ON be.BEID = ibg.FKBEID LEFT JOIN IndexBenutzerModule ibm ON ibm.FKBEID = be.BEID OR ibm.FKBEID = ibg.FKG LEFT JOIN Module md ON ibm.ModuleId = md.MID AND ISNULL(md.Sichtbar,0) = 1 WHERE be.BEID = @BEID", connection);
            command.Parameters.AddWithValue("@BEID", BEID);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var modules = new List<int>();
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    modules.Add(Convert.ToInt32(reader.GetValue(0)));
                }
            }

            return modules;
        }

        private async Task<AuthResponse> LoginLegacyAsync(SqlConnection connection, string szName, string szPW, CancellationToken cancellationToken)
        {
            await using var userCommand = new SqlCommand(
                "SELECT TOP 1 BEID, Benutzername, Email, ISNULL(IsDeactivated, 0), ISNULL(LoginCount, 0), ISNULL(LastLogin, CONVERT(datetime, '20000101')), Passwort, ISNULL(Benutzertyp, 9), ISNULL(Admin, 0), ISNULL(Kosoz, 0) FROM [Benutzer] WHERE Benutzername = @username",
                connection);
            userCommand.Parameters.AddWithValue("@username", szName);
            await using var reader = await userCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new AuthResponse { UserId = 0, LoginStatus = 0 };
            }

            var userId = reader.GetInt32(0);
            var isDeactivated = Convert.ToBoolean(reader.GetValue(3));
            var loginCount = Convert.ToInt32(reader.GetValue(4));
            var lastLogin = Convert.ToDateTime(reader.GetValue(5));
            var waitTime = loginCount >= 12 ? 600 : loginCount >= 10 ? 300 : loginCount >= 5 ? 120 : 0;
            var lastLoginSecondsToNow = (int)(DateTime.Now - lastLogin).TotalSeconds;

            if ((loginCount >= 5 && lastLoginSecondsToNow <= waitTime) || isDeactivated)
            {
                if (isDeactivated)
                {
                    return new AuthResponse { UserId = -1, LoginStatus = -1 };
                }

                var waitSeconds = (waitTime - (lastLoginSecondsToNow - 10)) * -1;
                if (waitSeconds < 0)
                {
                    return new AuthResponse { UserId = waitSeconds, LoginStatus = waitSeconds };
                }
            }

            var storedPassword = reader.IsDBNull(6) ? string.Empty : Convert.ToString(reader.GetValue(6)) ?? string.Empty;
            var username = Convert.ToString(reader.GetValue(1)) ?? szName;
            var email = reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2));
            var userType = Convert.ToInt32(reader.GetValue(7));
            var isAdmin = Convert.ToBoolean(reader.GetValue(8));
            var isKosoz = Convert.ToBoolean(reader.GetValue(9));
            var passwordMatches = string.Equals(storedPassword, szPW, StringComparison.Ordinal);
            if (!passwordMatches)
            {
                try
                {
                    passwordMatches = LegacyCrypto.AesDecode(storedPassword) == szPW;
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    passwordMatches = false;
                }
                catch (FormatException)
                {
                    passwordMatches = false;
                }
            }
            var newLoginCount = loginCount;
            var loginStatus = userId;
            if (!passwordMatches)
            {
                newLoginCount++;
                if (newLoginCount > 5)
                {
                    loginStatus = (waitTime + 10) * -1;
                }

                if (newLoginCount >= 15)
                {
                    loginStatus = -1;
                    isDeactivated = true;
                }
            }
            else
            {
                newLoginCount = 0;
            }

            reader.Close();
            await using var updateCommand = new SqlCommand(
                "UPDATE [Benutzer] SET LoginCount = @loginCount, LastLogin = GETDATE(), IsDeactivated = @isDeactivated WHERE Benutzername = @username",
                connection);
            updateCommand.Parameters.AddWithValue("@loginCount", newLoginCount);
            updateCommand.Parameters.AddWithValue("@isDeactivated", isDeactivated);
            updateCommand.Parameters.AddWithValue("@username", szName);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            var modules = loginStatus > 0
                ? await GetUserModulesAsync(connection, userId, cancellationToken)
                : Array.Empty<int>();
            return loginStatus > 0
                ? new AuthResponse { UserId = userId, LoginStatus = loginStatus, Username = username, Email = email, IsDeactivated = isDeactivated, UserType = userType, IsAdmin = isAdmin, IsKosoz = isKosoz, Modules = modules }
                : new AuthResponse { UserId = loginStatus, LoginStatus = loginStatus, IsDeactivated = isDeactivated };
        }

        public async Task SendPasswordRequestEmailAsync(string username, PasswordRequestResponse request, string url, CancellationToken cancellationToken)
        {
            if (!request.Requested || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Hash))
            {
                return;
            }

            var settings = await GetMailSettingsAsync(cancellationToken);
            if (settings is null || string.IsNullOrWhiteSpace(settings.MailServerAddress) ||
                string.IsNullOrWhiteSpace(settings.MailServerEmail) || settings.MailServerPort <= 0)
            {
                return;
            }

            var szSbubject = "TOPqw365 - Passwort zurücksetzen";
            var szBody = $"Sehr geehrter TOPqw365-Benutzer ({username}),\r\n" +
                "Sie haben eine Passwortaktualisierung angefordert. Zum Aktualisieren des Passwortes klicken Sie bitte auf folgenden Link. Der Link ist eine halbe Stunde lang gültig.\r\n" +
                url + "\r\n\r\n" +
                "Sollten Sie diese E-Mail nicht veranlasst haben, melden Sie dies bitte umgehend der Administration.";

            using var message = new MailMessage(settings.MailServerEmail, request.Email, szSbubject, szBody);
            using var client = new SmtpClient(settings.MailServerAddress, settings.MailServerPort)
            {
                EnableSsl = settings.MailServerMitSSL,
                Credentials = new NetworkCredential(settings.MailServerEmail, settings.MailServerPassword ?? string.Empty)
            };
            await client.SendMailAsync(message, cancellationToken);
        }

        public async Task<int> CheckPasswordRequestHashAsync(string hash, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT TOP 1 FKUSID, RequestDate FROM TOPqw_PasswordRequest WHERE Token = @hash ORDER BY RequestDate DESC", connection);
            command.Parameters.AddWithValue("@hash", hash);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return 0;
            }

            return DateTime.Now - Convert.ToDateTime(reader.GetValue(1)) <= TimeSpan.FromMinutes(30)
                ? Convert.ToInt32(reader.GetValue(0))
                : 0;
        }

        public async Task ChangePasswordAsync(int uid, string password, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("dbo.SaveUserPassword", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@uid", uid);
            command.Parameters.AddWithValue("@pass", password);
            command.Parameters.AddWithValue("@symKeyPass", InternalDecryptionKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<bool> VerifyCurrentPasswordAsync(int uid, string password, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var useSaltedHash = await GetSaltedHashOptionAsync(connection, cancellationToken);

            if (useSaltedHash)
            {
                await using var userCommand = new SqlCommand("SELECT Benutzername FROM [Benutzer] WHERE BEID = @beid", connection);
                userCommand.Parameters.AddWithValue("@beid", uid);
                var username = Convert.ToString(await userCommand.ExecuteScalarAsync(cancellationToken));
                if (string.IsNullOrWhiteSpace(username)) return false;

                await using var loginCommand = new SqlCommand("[dbo].[LoginUser]", connection)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                loginCommand.Parameters.AddWithValue("@username", username);
                loginCommand.Parameters.AddWithValue("@pw", password);
                loginCommand.Parameters.AddWithValue("@symKey", LegacyCrypto.AesEncode(password));
                var result = await loginCommand.ExecuteScalarAsync(cancellationToken);
                return result is not null && result != DBNull.Value && Convert.ToInt32(result) == uid;
            }

            await using var passwordCommand = new SqlCommand("SELECT Passwort FROM [Benutzer] WHERE BEID = @beid", connection);
            passwordCommand.Parameters.AddWithValue("@beid", uid);
            var storedPassword = Convert.ToString(await passwordCommand.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
            return string.Equals(storedPassword, password, StringComparison.Ordinal) ||
                   TryDecodePassword(storedPassword, password);
        }

        private static bool TryDecodePassword(string storedPassword, string password)
        {
            try
            {
                return LegacyCrypto.AesDecode(storedPassword) == password;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return false;
            }
        }

        public async Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT TOP 1 WertTXT FROM TOPqw_Einstellungen WHERE Einstellung = @bezeichnung AND ISNULL(fkUSID, 0) = 0", connection);
            command.Parameters.AddWithValue("@bezeichnung", "PasswordSecuritySyntax");
            var value = await command.ExecuteScalarAsync(cancellationToken);
            var parts = value is null || value == DBNull.Value ? Array.Empty<string>() : Convert.ToString(value)?.Split(';') ?? Array.Empty<string>();
            if (parts.Length != 5 || !int.TryParse(parts[0], out var minVal) || !int.TryParse(parts[1], out var maxVal) ||
                !int.TryParse(parts[2], out var withSizedLetters) || !int.TryParse(parts[3], out var withSonderzeichen) ||
                !int.TryParse(parts[4], out var withZiffern))
            {
                return new PasswordPolicy();
            }

            return new PasswordPolicy
            {
                MinVal = minVal,
                MaxVal = maxVal,
                WithSizedLetters = withSizedLetters,
                WithSonderzeichen = withSonderzeichen,
                WithZiffern = withZiffern
            };
        }

        public async Task<PasswordRequestResponse> RequestPasswordAsync(string username, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var existingCommand = new SqlCommand(
                "SELECT TOP 1 Requestdate FROM TOPqw_PasswordRequest INNER JOIN [Benutzer] ON TOPqw_PasswordRequest.FKUSID = [Benutzer].BEID AND Benutzername = @username ORDER BY Requestdate DESC",
                connection);
            existingCommand.Parameters.AddWithValue("@username", username);
            var requestDateValue = await existingCommand.ExecuteScalarAsync(cancellationToken);
            if (requestDateValue is not null && requestDateValue != DBNull.Value)
            {
                var waitSeconds = (int)Math.Ceiling(TimeSpan.FromMinutes(5).TotalSeconds - (DateTime.Now - Convert.ToDateTime(requestDateValue)).TotalSeconds);
                if (waitSeconds > 0)
                {
                    return new PasswordRequestResponse { WaitSeconds = waitSeconds };
                }
            }

            await using var requestCommand = new SqlCommand("CreatePasswordRequest", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            requestCommand.Parameters.AddWithValue("@username", username);
            var hashValue = await requestCommand.ExecuteScalarAsync(cancellationToken);
            var hash = hashValue is null || hashValue == DBNull.Value ? null : Convert.ToString(hashValue);
            if (string.IsNullOrEmpty(hash) || hash.Length != 32)
            {
                return new PasswordRequestResponse();
            }

            await using var emailCommand = new SqlCommand(
                "SELECT Email FROM [Benutzer] WHERE Benutzername = @username",
                connection);
            emailCommand.Parameters.AddWithValue("@username", username);
            var emailValue = await emailCommand.ExecuteScalarAsync(cancellationToken);
            return new PasswordRequestResponse
            {
                Requested = true,
                Hash = hash,
                Email = emailValue is null || emailValue == DBNull.Value ? null : Convert.ToString(emailValue)
            };
        }

        public async Task<string?> GetMailToAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(
                "SELECT TOP 1 WertTXT FROM TOPqw_Einstellungen WHERE Einstellung = @bezeichnung AND ISNULL(fkUSID, 0) = 0",
                connection);
            command.Parameters.AddWithValue("@bezeichnung", "qmWeb.Mailto");
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value == DBNull.Value ? null : Convert.ToString(value);
        }

        private async Task<bool> GetSaltedHashOptionAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(
                "SELECT TOP 1 WertBOOL FROM TOPqw_Einstellungen WHERE Einstellung = @bezeichnung AND ISNULL(fkUSID, 0) = 0",
                connection);
            command.Parameters.AddWithValue("@bezeichnung", "SaltedHash-Verfahren.nutzen");
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is not null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        private async Task<LegacyMailSettings?> GetMailSettingsAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT (SELECT TOP 1 WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = 'MailServerEmail') MailServerEmail, (SELECT TOP 1 WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = 'MailSupport') MailSupport, (SELECT TOP 1 WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = 'MailServerAddress') MailServerAddress, (SELECT TOP 1 WertBOOL FROM TOPqw_Einstellungen WHERE Einstellung = 'MailServerMitSSL') MailServerMitSSL, (SELECT TOP 1 WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = 'MailServerPassword') MailServerPassword, (SELECT TOP 1 WertMEMO FROM TOPqw_Einstellungen WHERE Einstellung = 'MailServerPort') MailServerPort", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new LegacyMailSettings
            {
                MailServerEmail = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0)),
                MailSupport = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1)),
                MailServerAddress = reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2)),
                MailServerMitSSL = !reader.IsDBNull(3) && Convert.ToBoolean(reader.GetValue(3)),
                MailServerPassword = reader.IsDBNull(4) ? null : Convert.ToString(reader.GetValue(4)),
                MailServerPort = reader.IsDBNull(5) || !int.TryParse(Convert.ToString(reader.GetValue(5)), out var port) ? 0 : port
            };
        }
    }

    internal static class LegacyCrypto
    {
        public static string AesEncode(string value)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            var publicKey = new string("•–—’˜™š›œÅÆÇÎ¯°…ÑÒ³Ô‘".Select(character => (char)((character - 100 + 255) % 255)).ToArray());
            var salt = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(publicKey));
            using var deriveBytes = new System.Security.Cryptography.Rfc2898DeriveBytes(publicKey, salt);
            aes.Key = deriveBytes.GetBytes(16);
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
            aes.GenerateIV();
            using var stream = new MemoryStream();
            stream.Write(aes.IV);
            using (var cryptoStream = new System.Security.Cryptography.CryptoStream(stream, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write, true))
            using (var writer = new StreamWriter(cryptoStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(value);
            }

            return Convert.ToBase64String(stream.ToArray());
        }

        public static string AesDecode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            using var aes = System.Security.Cryptography.Aes.Create();
            var publicKey = new string("•–—’˜™š›œÅÆÇÎ¯°…ÑÒ³Ô‘".Select(character => (char)((character - 100 + 255) % 255)).ToArray());
            var salt = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(publicKey));
            using var deriveBytes = new System.Security.Cryptography.Rfc2898DeriveBytes(publicKey, salt);
            aes.Key = deriveBytes.GetBytes(16);
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

            var encrypted = Convert.FromBase64String(value);
            var ivLength = aes.BlockSize / 8;
            var iv = encrypted[..ivLength];
            aes.IV = iv;
            using var stream = new MemoryStream(encrypted, ivLength, encrypted.Length - ivLength);
            using var cryptoStream = new System.Security.Cryptography.CryptoStream(stream, aes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
