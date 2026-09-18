const form = document.getElementById("settings");
const banner = document.getElementById("banner");
const customFields = document.getElementById("custom");
const presetSummary = document.getElementById("preset-summary");
let presets = [];
const toolRows = { "yt-dlp": "ytDlp", ffmpeg: "ffmpeg" };

function say(text, isError = false) {
  banner.textContent = text;
  banner.hidden = false;
  banner.classList.toggle("is-error", isError);
}

function show(reply) {
  if (reply.type === "error") {
    say(reply.message, true);
    return;
  }
  for (const [name, key] of Object.entries(toolRows)) {
    const row = document.querySelector(`[data-tool="${name}"]`);
    const tool = reply[key];
    const state = row.querySelector(".state");
    state.textContent = tool ? `${tool.version ?? t("unknownVersion")} (${tool.path})` : t("notFound");
    state.classList.toggle("is-missing", !tool);
    row.querySelector(".install").textContent = t(tool ? "update" : "download");
  }
  const { settings, limits } = reply;
  presets = reply.presets;
  form.ytDlpPath.value = settings.ytDlpPath ?? "";
  form.ffmpegPath.value = settings.ffmpegPath ?? "";
  form.outputDir.value = settings.outputDir;
  form.preset.value = settings.preset;
  form.width.value = settings.custom.width;
  form.fps.value = settings.custom.fps;
  form.colors.value = settings.custom.colors;
  form.dither.value = settings.custom.dither;
  form.palettePerFrame.checked = settings.custom.palettePerFrame;
  form.width.min = limits.minWidth;
  form.width.max = limits.maxWidth;
  form.fps.min = limits.minFps;
  form.fps.max = limits.maxFps;
  showPreset();
}

function showPreset() {
  const preset = presets.find((p) => p.id === form.preset.value);
  customFields.hidden = !!preset;
  presetSummary.textContent = preset
    ? t("presetSummary", String(preset.quality.width), String(preset.quality.fps), String(preset.quality.colors))
    : "";
}

async function install(row) {
  const button = row.querySelector(".install");
  button.disabled = true;
  const reply = await callHost({ type: "install-tool", tool: row.dataset.tool }, (progress) => {
    button.textContent = progress.percent == null ? t("downloading") : `${progress.percent}%`;
  });
  button.disabled = false;
  show(reply);
  if (reply.type !== "error") say(t("toolReady", row.dataset.tool));
}

for (const row of document.querySelectorAll(".tool")) {
  row.querySelector(".install").addEventListener("click", () => install(row));
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const reply = await callHost({
    type: "set-settings",
    settings: {
      ytDlpPath: form.ytDlpPath.value.trim(),
      ffmpegPath: form.ffmpegPath.value.trim(),
      outputDir: form.outputDir.value.trim(),
      preset: form.preset.value,
      custom: {
        width: form.width.valueAsNumber,
        fps: form.fps.valueAsNumber,
        colors: Number(form.colors.value),
        dither: form.dither.value,
        palettePerFrame: form.palettePerFrame.checked,
      },
    },
  });
  show(reply);
  if (reply.type !== "error") say(t("settingsSaved"));
});

form.preset.addEventListener("change", showPreset);

translatePage();
callHost({ type: "status" }).then(show);
