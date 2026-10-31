// Короткая запись «создай элемент». text всегда кладётся как текст, не как HTML.
export function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) {
        node.className = className;
    }

    if (text !== undefined && text !== null) {
        node.textContent = String(text);
    }

    return node;
}

export function setBusy(button, busy, text) {
    if (busy) {
        if (!button.dataset.label) {
            button.dataset.label = button.textContent;
        }

        button.disabled = true;
        if (text) {
            button.textContent = text;
        }

        return;
    }

    button.disabled = false;
    if (button.dataset.label) {
        button.textContent = button.dataset.label;
        delete button.dataset.label;
    }
}
