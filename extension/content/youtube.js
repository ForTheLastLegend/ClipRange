const HEAVY_BYTES = 20 * 1024 * 1024;
const HEAVY_SECONDS = 30;
const GIF_BYTES_PER_PIXEL_FRAME = 0.5;

// YouTube enforces Trusted Types, which also blocks innerHTML from content scripts.
function playerButton(title, mark) {
  const button = document.createElement("button");
  button.className = "ytp-button";
  button.title = title;
  if (mark) button.dataset.mark = mark;
  return button;
}

const startButton = playerButton(t("startTitle"), "start");
const endButton = playerButton(t("endTitle"), "end");
const makeButton = playerButton(t("makeTitle"));
const controls = document.createElement("div");
controls.className = "cliprange";
controls.append(startButton, endButton, makeButton);

const statusLabel = document.createElement("span");
const statusFill = document.createElement("div");
const statusTrack = document.createElement("div");
statusTrack.className = "cliprange-status-track";
statusTrack.append(statusFill);
const status = document.createElement("div");
status.className = "cliprange-status";
status.hidden = true;
status.append(statusLabel, statusTrack);

const startMarker = document.createElement("div");
const endMarker = document.createElement("div");
const markers = document.createElement("div");
markers.className = "cliprange-markers";
markers.append(startMarker, endMarker);

let range = { start: null, end: null };
let job = null;
let armed = false;
let hostStatus = null;

const video = () => document.querySelector("video.html5-main-video");
const watchUrl = () => `https://www.youtube.com/watch?v=${new URLSearchParams(location.search).get("v")}`;

function clock(seconds) {
  const m = Math.floor(seconds / 60);
  const s = (seconds % 60).toFixed(1).padStart(4, "0");
  return `${m}:${s}`;
}

function render() {
  startButton.textContent = range.start === null ? t("start") : clock(range.start);
  endButton.textContent = range.end === null ? t("end") : clock(range.end);
  startButton.classList.toggle("is-set", range.start !== null);
  endButton.classList.toggle("is-set", range.end !== null);
  placeMarker(startMarker, range.start);
  placeMarker(endMarker, range.end);

  status.hidden = !job;
  if (job) {
    makeButton.disabled = true;
    makeButton.textContent = job.percent == null ? "..." : `${job.percent}%`;
    statusLabel.textContent = t(job.stage === "download" ? "stageDownload" : "stageEncode");
    statusFill.style.width = `${job.percent ?? 0}%`;
    statusTrack.classList.toggle("is-indeterminate", job.percent == null);
    return;
  }
  const valid = range.start !== null && range.end !== null && range.end > range.start;
  makeButton.disabled = !valid;
  makeButton.textContent = armed ? `~${Math.round(estimateBytes() / 1048576)} MB?` : t("make");
  makeButton.classList.toggle("is-armed", armed);
}

function placeMarker(marker, seconds) {
  const duration = video()?.duration;
  marker.hidden = seconds === null || !duration;
  if (!marker.hidden) marker.style.left = `${(seconds / duration) * 100}%`;
}

function estimateBytes() {
  const { width, fps } = hostStatus.quality;
  return width * (width * 9 / 16) * fps * (range.end - range.start) * GIF_BYTES_PER_PIXEL_FRAME;
}

// Keeps start < end: moving the start past the end clears the end, an end before the start is refused.
function setMark(mark, time) {
  if (mark === "start") {
    range = { start: time, end: range.end !== null && range.end <= time ? null : range.end };
  } else if (range.start !== null && time <= range.start) {
    notice(t("endBeforeStart"), true);
    return;
  } else {
    range = { ...range, end: time };
  }
  armed = false;
  render();
}

function notice(text, isError = false) {
  document.querySelector(".cliprange-notice")?.remove();
  const box = document.createElement("div");
  box.className = `cliprange-notice${isError ? " is-error" : ""}`;
  box.textContent = text;
  document.getElementById("movie_player")?.append(box);
  setTimeout(() => box.remove(), isError ? 8000 : 4000);
}

async function makeGif() {
  // After the extension is reloaded, scripts already injected lose their runtime.
  if (!chrome.runtime?.id) {
    return notice(t("staleScript"), true);
  }
  if (hostStatus === null) {
    const reply = await callHost({ type: "status" });
    if (reply.type === "error") return notice(reply.message, true);
    hostStatus = reply;
  }

  const duration = range.end - range.start;
  if (!(duration > 0)) {
    return notice(t("endBeforeStart"), true);
  }
  if (duration > hostStatus.limits.maxSeconds) {
    return notice(t("tooLong", String(hostStatus.limits.maxSeconds)), true);
  }
  if (!armed && (duration > HEAVY_SECONDS || estimateBytes() > HEAVY_BYTES)) {
    armed = true;
    render();
    notice(t("heavy"));
    setTimeout(() => { armed = false; render(); }, 6000);
    return;
  }

  armed = false;
  job = { stage: "download", percent: null };
  render();
  const reply = await callHost(
    { type: "make-gif", url: watchUrl(), start: range.start, end: range.end },
    (progress) => { job = progress; render(); });
  job = null;
  render();

  if (reply.type === "error") notice(reply.message, true);
  else notice(t("saved", reply.path.split("\\").pop()));
}

controls.addEventListener("click", (event) => {
  const button = event.target.closest("button");
  if (!button) return;
  if (button.dataset.mark) {
    const time = video()?.currentTime;
    if (time !== undefined) setMark(button.dataset.mark, time);
  } else if (!job) {
    makeGif();
  }
});

// The player's right control bar is shared with other extensions, so the buttons go in
// as an ordinary inline sibling and are re-inserted whenever YouTube rebuilds the bar.
function mount() {
  if (!status.isConnected) {
    document.querySelector("ytd-masthead #center")?.append(status);
  }
  if (!location.pathname.startsWith("/watch")) {
    controls.remove();
    markers.remove();
    return;
  }
  if (controls.isConnected && markers.isConnected) return;

  const settings = document.querySelector(".ytp-right-controls .ytp-settings-button");
  const progressBar = document.querySelector(".ytp-progress-bar");
  if (!settings || !progressBar) return;
  settings.parentElement.insertBefore(controls, settings);
  progressBar.append(markers);
  render();
}

document.addEventListener("yt-navigate-finish", () => {
  range = { start: null, end: null };
  armed = false;
  mount();
});
new MutationObserver(mount).observe(document.documentElement, { childList: true, subtree: true });
mount();
