const HOST = "com.cliprange.host";
const t = (key) => chrome.i18n.getMessage(key);

// Host error codes with a translation; other messages are shown as the host wrote them.
const translatedErrors = { "tool-missing": "errorToolMissing" };

function callNative(request, onProgress) {
  return new Promise((resolve) => {
    const port = chrome.runtime.connectNative(HOST);
    port.onMessage.addListener((reply) => {
      if (reply.type === "progress") {
        onProgress(reply);
        return;
      }
      port.disconnect();
      if (reply.type === "error" && reply.code in translatedErrors) {
        reply = { ...reply, message: t(translatedErrors[reply.code]) };
      }
      resolve(reply);
    });
    port.onDisconnect.addListener(() => {
      const raw = chrome.runtime.lastError?.message;
      let message;
      if (raw === "Specified native messaging host not found.") {
        message = t("hostMissing");
      } else {
        message = raw || t("hostSilent");
      }
      resolve({ type: "error", code: "host", message });
    });
    port.postMessage(request);
  });
}

chrome.runtime.onConnect.addListener((port) => {
  let open = true;
  port.onDisconnect.addListener(() => { open = false; });

  port.onMessage.addListener(async (request) => {
    const reply = await callNative(request, (progress) => open && port.postMessage(progress));
    if (request.type === "make-gif" && reply.type === "done") {
      await notifyDone(reply.path);
    }
    if (open) port.postMessage(reply);
  });
});

async function notifyDone(path) {
  const id = await chrome.notifications.create({
    type: "basic",
    iconUrl: "icons/128.png",
    title: t("notificationTitle"),
    message: path.split("\\").pop(),
    buttons: [{ title: t("notificationOpen") }, { title: t("notificationFolder") }],
  });
  await chrome.storage.session.set({ [id]: path });
}

async function openResult(id, folder) {
  const { [id]: path } = await chrome.storage.session.get(id);
  chrome.notifications.clear(id);
  if (path) callNative({ type: "open", path, folder }, () => {});
}

chrome.notifications.onClicked.addListener((id) => openResult(id, false));
chrome.notifications.onButtonClicked.addListener((id, button) => openResult(id, button === 1));

chrome.runtime.onInstalled.addListener(({ reason }) => {
  if (reason === "install") chrome.runtime.openOptionsPage();
});
