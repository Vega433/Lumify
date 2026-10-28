import { authApi } from "../api.js";
import { el, setBusy } from "../dom.js";
import { navigate, takeNext } from "../navigation.js";
import { createAlert } from "../components/alert.js";
import { createAuthShell } from "../components/auth-shell.js";
import { createField } from "../components/field.js";

function latestBirthDate() {
    const date = new Date();
    date.setFullYear(date.getFullYear() - 12);
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${date.getFullYear()}-${month}-${day}`;
}

export function mountRegisterPage() {
    const footer = el("p", "hint");
    footer.append("Уже есть аккаунт? ");
    const link = document.createElement("a");
    link.className = "text-link";
    link.href = "#/login";
    link.textContent = "Вход";
    footer.append(link);

    const { element, form } = createAuthShell({
        title: "Регистрация",
        text: "После создания аккаунта сразу попадёшь внутрь, отдельно входить не нужно.",
        footer
    });

    const alert = createAlert();
    const button = document.createElement("button");
    button.className = "button";
    button.type = "submit";
    button.textContent = "Создать аккаунт";

    form.append(
        createField({
            label: "Имя",
            name: "userName",
            required: true,
            minLength: 4,
            maxLength: 100,
            autocomplete: "nickname"
        }),
        createField({
            label: "Почта",
            name: "email",
            type: "email",
            required: true,
            autocomplete: "email"
        }),
        createField({
            label: "Пароль",
            name: "password",
            type: "password",
            required: true,
            minLength: 6,
            autocomplete: "new-password"
        }),
        createField({
            label: "Дата рождения",
            name: "birthDate",
            type: "date",
            required: true,
            min: "1900-01-01",
            max: latestBirthDate()
        }),
        createField({
            label: "Аватар, можно не выбирать",
            name: "avatar",
            type: "file",
            accept: ".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
        }),
        el("p", "hint", "Имя от 4 символов, пароль от 6, возраст от 12 лет. Аватар: jpg, png или webp."),
        alert.element,
        button
    );

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();

        const userName = form.querySelector("input[name=userName]").value.trim();
        const email = form.querySelector("input[name=email]").value.trim();
        const password = form.querySelector("input[name=password]").value;
        const birthDate = form.querySelector("input[name=birthDate]").value;
        const avatar = form.querySelector("input[name=avatar]").files[0];

        const payload = new FormData();
        payload.set("userName", userName);
        payload.set("email", email);
        payload.set("password", password);
        payload.set("birthDate", birthDate);
        if (avatar) {
            payload.set("avatar", avatar);
        }

        setBusy(button, true, "Создаём…");
        try {
            await authApi.register(payload);
            await authApi.login(email, password);
            navigate(takeNext());
        } catch (error) {
            alert.show(error.message);
            setBusy(button, false);
        }
    });

    return { element, destroy() { } };
}
