import { authApi } from "../api.js";
import { el, setBusy } from "../dom.js";
import { navigate, takeNext } from "../navigation.js";
import { createAlert } from "../components/alert.js";
import { createAuthShell } from "../components/auth-shell.js";
import { createField } from "../components/field.js";

export function mountLoginPage() {
    const footer = el("p", "hint");
    footer.append("Нет аккаунта? ");
    const link = document.createElement("a");
    link.className = "text-link";
    link.href = "#/register";
    link.textContent = "Регистрация";
    footer.append(link);

    const { element, form } = createAuthShell({
        title: "Вход",
        text: "Почта и пароль от аккаунта Lumify.",
        footer
    });

    const alert = createAlert();
    const button = document.createElement("button");
    button.className = "button";
    button.type = "submit";
    button.textContent = "Войти";

    form.append(
        createField({
            label: "Почта",
            name: "email",
            type: "email",
            required: true,
            autocomplete: "username"
        }),
        createField({
            label: "Пароль",
            name: "password",
            type: "password",
            required: true,
            minLength: 6,
            autocomplete: "current-password"
        }),
        alert.element,
        button
    );

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();
        const email = form.querySelector("input[name=email]").value.trim();
        const password = form.querySelector("input[name=password]").value;
        setBusy(button, true, "Входим…");
        try {
            await authApi.login(email, password);
            navigate(takeNext());
        } catch (error) {
            alert.show(error.message);
            setBusy(button, false);
        }
    });

    return { element, destroy() { } };
}
