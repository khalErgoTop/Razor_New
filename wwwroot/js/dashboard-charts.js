(() => {
    const colors = ["#1f4e79", "#70ad47", "#ed7d31", "#a5a5a5", "#ffc000", "#4472c4"];

    function readValues(element) {
        try {
            const labels = JSON.parse(element.dataset.dashboardLabels || "[]");
            const values = JSON.parse(element.dataset.dashboardValues || "[]");
            return labels.map((label, index) => ({
                label: String(label ?? ""),
                value: Number(values[index])
            })).filter(item => Number.isFinite(item.value));
        } catch {
            return [];
        }
    }

    function createSvg(width, height) {
        const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("viewBox", `0 0 ${width} ${height}`);
        svg.setAttribute("role", "presentation");
        svg.setAttribute("width", "100%");
        svg.setAttribute("height", String(height));
        return svg;
    }

    function addText(svg, x, y, text, anchor = "middle") {
        const node = document.createElementNS("http://www.w3.org/2000/svg", "text");
        node.setAttribute("x", String(x));
        node.setAttribute("y", String(y));
        node.setAttribute("text-anchor", anchor);
        node.textContent = text;
        svg.appendChild(node);
    }

    function renderBarOrLine(element, items, type) {
        const width = 720;
        const height = 360;
        const left = 52;
        const bottom = 54;
        const top = 20;
        const right = 20;
        const chartWidth = width - left - right;
        const chartHeight = height - top - bottom;
        const max = Math.max(...items.map(item => item.value), 1);
        const svg = createSvg(width, height);
        const axis = document.createElementNS("http://www.w3.org/2000/svg", "path");
        axis.setAttribute("d", `M ${left} ${top} V ${top + chartHeight} H ${left + chartWidth}`);
        axis.setAttribute("fill", "none");
        axis.setAttribute("stroke", "currentColor");
        svg.appendChild(axis);

        const points = [];
        items.forEach((item, index) => {
            const x = left + (index + 0.5) * chartWidth / items.length;
            const y = top + chartHeight - item.value / max * chartHeight;
            points.push(`${x},${y}`);
            if (type === "bar") {
                const bar = document.createElementNS("http://www.w3.org/2000/svg", "rect");
                const barWidth = Math.max(chartWidth / items.length * 0.65, 4);
                bar.setAttribute("x", String(x - barWidth / 2));
                bar.setAttribute("y", String(y));
                bar.setAttribute("width", String(barWidth));
                bar.setAttribute("height", String(top + chartHeight - y));
                bar.setAttribute("fill", colors[index % colors.length]);
                svg.appendChild(bar);
            } else {
                const point = document.createElementNS("http://www.w3.org/2000/svg", "circle");
                point.setAttribute("cx", String(x));
                point.setAttribute("cy", String(y));
                point.setAttribute("r", "4");
                point.setAttribute("fill", colors[0]);
                svg.appendChild(point);
            }
            addText(svg, x, height - 28, item.label);
        });

        if (type === "line" && points.length > 1) {
            const line = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
            line.setAttribute("points", points.join(" "));
            line.setAttribute("fill", "none");
            line.setAttribute("stroke", colors[0]);
            line.setAttribute("stroke-width", "3");
            svg.insertBefore(line, svg.lastChild);
        }

        element.replaceChildren(svg);
    }

    function renderPie(element, items) {
        const size = 360;
        const center = size / 2;
        const radius = 130;
        const total = items.reduce((sum, item) => sum + Math.max(item.value, 0), 0);
        const svg = createSvg(size, size);
        let angle = -Math.PI / 2;
        items.forEach((item, index) => {
            const fraction = total > 0 ? Math.max(item.value, 0) / total : 0;
            const end = angle + fraction * Math.PI * 2;
            const largeArc = end - angle > Math.PI ? 1 : 0;
            const x1 = center + radius * Math.cos(angle);
            const y1 = center + radius * Math.sin(angle);
            const x2 = center + radius * Math.cos(end);
            const y2 = center + radius * Math.sin(end);
            const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
            path.setAttribute("d", `M ${center} ${center} L ${x1} ${y1} A ${radius} ${radius} 0 ${largeArc} 1 ${x2} ${y2} Z`);
            path.setAttribute("fill", colors[index % colors.length]);
            path.setAttribute("aria-label", `${item.label}: ${item.value}`);
            svg.appendChild(path);
            angle = end;
        });
        element.replaceChildren(svg);
    }

    document.querySelectorAll("[data-dashboard-chart]").forEach(element => {
        const items = readValues(element);
        if (!items.length) return;
        const type = (element.dataset.dashboardChart || "bar").toLowerCase();
        if (type === "pie") renderPie(element, items);
        else if (type === "bar" || type === "line") renderBarOrLine(element, items, type);
    });
})();
