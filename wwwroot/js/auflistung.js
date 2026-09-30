document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".sc00, .sc01").forEach(function (element) {
        element.addEventListener("click", function () {
            var list = element.nextElementSibling;
            if (!list || list.tagName.toLowerCase() !== "ul") return;
            var isHidden = window.getComputedStyle(list).display === "none";
            list.style.display = isHidden ? "block" : "none";
            element.setAttribute("aria-expanded", isHidden ? "true" : "false");
        });
        element.addEventListener("keydown", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                element.click();
            }
        });
    });

    document.querySelectorAll(".wtSttToggle").forEach(function (element) {
        element.addEventListener("click", function () {
            var list = element.nextElementSibling;
            if (!list || list.tagName.toLowerCase() !== "ul") return;
            var isHidden = window.getComputedStyle(list).display === "none";
            list.style.display = isHidden ? "block" : "none";
            element.setAttribute("aria-expanded", isHidden ? "true" : "false");
        });
        element.addEventListener("keydown", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                element.click();
            }
        });
    });
});