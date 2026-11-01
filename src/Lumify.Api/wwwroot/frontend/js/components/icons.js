const svgNs = "http://www.w3.org/2000/svg";

function svg(path, className) {
    const node = document.createElementNS(svgNs, "svg");
    node.setAttribute("viewBox", "0 0 24 24");
    node.setAttribute("aria-hidden", "true");
    node.classList.add("icon", className);
    const shape = document.createElementNS(svgNs, "path");
    shape.setAttribute("d", path);
    node.append(shape);
    return node;
}

export function iconHeart(className = "icon--heart") {
    return svg("M12 20s-7-4.35-7-9a4 4 0 0 1 7-2 4 4 0 0 1 7 2c0 4.65-7 9-7 9z", className);
}

export function iconEye(className = "icon--eye") {
    return svg("M2.8 12S6.2 6.5 12 6.5 21.2 12 21.2 12 17.8 17.5 12 17.5 2.8 12 2.8 12zm9.2 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6z", className);
}
