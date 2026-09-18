// Shared by the content script and the options page: sends one request to the
// service worker, which relays it to the native host, and resolves with the final reply.
function callHost(request, onProgress) {
  return new Promise((resolve) => {
    const port = chrome.runtime.connect({ name: "host" });
    port.onMessage.addListener((reply) => {
      if (reply.type === "progress") {
        onProgress?.(reply);
        return;
      }
      port.disconnect();
      resolve(reply);
    });
    port.onDisconnect.addListener(() => {
      resolve({ type: "error", code: "extension", message: t("backgroundStopped") });
    });
    port.postMessage(request);
  });
}
