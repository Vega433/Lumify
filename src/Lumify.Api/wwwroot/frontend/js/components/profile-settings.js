import { usersApi } from "../api.js";
import { el, setBusy } from "../dom.js";
import { navigate } from "../navigation.js";
import { clearSession, updateSessionUser } from "../session.js";
import { createAlert } from "./alert.js";
import { createField } from "./field.js";

function latestBirthDate() {
    const date = new Date();
    date.setFullYear(date.getFullYear() - 12);
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${date.getFullYear()}-${month}-${day}`;
}

function birthValue(value) {
    if (!value) {
        return "";
    }

    return String(value).slice(0, 10);
}

export function createProfileSettings(profile, onProfile) {
    const root = el("section", "tile stack");
    const alert = createAlert();
    root.append(el("h2", "tile__title", "Аккаунт"), alert.element);

    const profileForm = document.createElement("form");
    profileForm.className = "form";
    profileForm.append(
        createField({
            label: "Имя",
            name: "userName",
            required: true,
            minLength: 4,
            maxLength: 80,
            value: profile.userName
        }),
        createField({
            label: "Почта",
            name: "email",
            type: "email",
            required: true,
            value: profile.email,
            autocomplete: "email"
        }),
        createField({
            label: "Дата рождения",
            name: "birthDate",
            type: "date",
            required: true,
            max: latestBirthDate(),
            value: birthValue(profile.birthDate)
        })
    );
    const saveProfile = document.createElement("button");
    saveProfile.className = "button button--small";
    saveProfile.type = "submit";
    saveProfile.textContent = "Сохранить профиль";
    profileForm.append(saveProfile);

    profileForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();
        setBusy(saveProfile, true, "Сохраняем…");
        try {
            const next = await usersApi.update({
                userName: profileForm.querySelector("[name=userName]").value.trim(),
                email: profileForm.querySelector("[name=email]").value.trim(),
                birthDate: profileForm.querySelector("[name=birthDate]").value
            });
            updateSessionUser(next);
            onProfile(next);
            alert.show("Профиль обновлён", "ok");
        } catch (error) {
            alert.show(error.message);
        } finally {
            setBusy(saveProfile, false);
        }
    });

    const passwordForm = document.createElement("form");
    passwordForm.className = "form";
    passwordForm.append(
        el("h3", "subhead", "Пароль"),
        createField({
            label: "Текущий пароль",
            name: "currentPassword",
            type: "password",
            required: true,
            autocomplete: "current-password"
        }),
        createField({
            label: "Новый пароль",
            name: "newPassword",
            type: "password",
            required: true,
            minLength: 6,
            autocomplete: "new-password"
        })
    );
    const savePassword = document.createElement("button");
    savePassword.className = "button button--small button--ghost";
    savePassword.type = "submit";
    savePassword.textContent = "Сменить пароль";
    passwordForm.append(savePassword);
    passwordForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();
        setBusy(savePassword, true, "Сохраняем…");
        try {
            const next = await usersApi.update({
                currentPassword: passwordForm.querySelector("[name=currentPassword]").value,
                newPassword: passwordForm.querySelector("[name=newPassword]").value
            });
            updateSessionUser(next);
            passwordForm.reset();
            alert.show("Пароль изменён", "ok");
        } catch (error) {
            alert.show(error.message);
        } finally {
            setBusy(savePassword, false);
        }
    });

    const avatarForm = document.createElement("form");
    avatarForm.className = "form";
    avatarForm.append(
        el("h3", "subhead", "Аватар"),
        createField({
            label: "jpg, png или webp",
            name: "avatar",
            type: "file",
            accept: ".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp",
            required: true
        })
    );
    const avatarActions = el("div", "actions");
    const saveAvatar = document.createElement("button");
    saveAvatar.className = "button button--small";
    saveAvatar.type = "submit";
    saveAvatar.textContent = "Заменить аватар";
    avatarActions.append(saveAvatar);

    const removeAvatar = document.createElement("button");
    removeAvatar.type = "button";
    removeAvatar.className = "button button--small button--ghost";
    removeAvatar.textContent = "Убрать аватар";
    removeAvatar.hidden = !profile.avatarUrl;
    removeAvatar.addEventListener("click", async () => {
        alert.hide();
        setBusy(removeAvatar, true, "Убираем…");
        try {
            const next = await usersApi.removeAvatar();
            updateSessionUser(next);
            removeAvatar.hidden = !next.avatarUrl;
            onProfile(next);
            alert.show("Аватар убран", "ok");
        } catch (error) {
            alert.show(error.message);
        } finally {
            setBusy(removeAvatar, false);
        }
    });
    avatarActions.append(removeAvatar);
    avatarForm.append(avatarActions);

    avatarForm.addEventListener("submit", async (event) => {
        event.preventDefault();
        alert.hide();
        const file = avatarForm.querySelector("[name=avatar]").files[0];
        if (!file) {
            alert.show("Выбери изображение");
            return;
        }

        const payload = new FormData();
        payload.set("avatar", file);
        setBusy(saveAvatar, true, "Загружаем…");
        try {
            const next = await usersApi.changeAvatar(payload);
            updateSessionUser(next);
            removeAvatar.hidden = !next.avatarUrl;
            avatarForm.reset();
            onProfile(next);
            alert.show("Аватар обновлён", "ok");
        } catch (error) {
            alert.show(error.message);
        } finally {
            setBusy(saveAvatar, false);
        }
    });

    const removeAccount = document.createElement("button");
    removeAccount.type = "button";
    removeAccount.className = "button button--small button--danger";
    removeAccount.textContent = "Удалить аккаунт";
    removeAccount.addEventListener("click", async () => {
        const nameNow = profileForm.querySelector("[name=userName]").value.trim() || profile.userName;
        const typed = window.prompt(`Удалить аккаунт «${nameNow}»? Напиши имя, чтобы подтвердить.`);
        if (typed === null) {
            return;
        }

        if (typed.trim() !== nameNow) {
            alert.show("Имя не совпало, аккаунт на месте");
            return;
        }

        alert.hide();
        setBusy(removeAccount, true, "Удаляем…");
        try {
            await usersApi.remove(profile.userId);
            clearSession();
            navigate("#/");
        } catch (error) {
            alert.show(error.message);
            setBusy(removeAccount, false);
        }
    });

    root.append(profileForm, passwordForm, avatarForm, removeAccount);
    return root;
}
