import { el } from "../dom.js";

export function createField(options) {
    const wrap = el("label", "field");
    wrap.append(el("span", "field__label", options.label));

    const multiline = options.type === "textarea";
    const input = document.createElement(multiline ? "textarea" : "input");
    input.className = "field__input";
    input.name = options.name;

    if (multiline) {
        input.rows = options.rows || 4;
    } else {
        input.type = options.type || "text";
    }

    if (options.required) {
        input.required = true;
    }

    if (options.minLength) {
        input.minLength = options.minLength;
    }

    if (options.maxLength) {
        input.maxLength = options.maxLength;
    }

    if (options.placeholder) {
        input.placeholder = options.placeholder;
    }

    if (options.accept) {
        input.accept = options.accept;
    }

    if (options.autocomplete) {
        input.autocomplete = options.autocomplete;
    }

    if (options.min) {
        input.min = options.min;
    }

    if (options.max) {
        input.max = options.max;
    }

    if (options.value !== undefined && options.value !== null) {
        input.value = options.value;
    }

    wrap.append(input);
    return wrap;
}
