import { createHeader } from "./components/header.js";
import { startRouter } from "./router.js";

const root = document.querySelector("#app");
const outlet = document.createElement("main");
outlet.className = "page";

root.append(createHeader(), outlet);
startRouter(outlet);
