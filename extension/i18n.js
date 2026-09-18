// Falls back to the key when the script has been orphaned by an extension reload.
const t = (key, ...substitutions) => chrome.i18n?.getMessage(key, substitutions) || key;

// Fills elements carrying data-i18n (text) or data-i18n-placeholder with their translation.
function translatePage() {
  document.title = t("optionsTitle");
  for (const el of document.querySelectorAll("[data-i18n]")) {
    el.textContent = t(el.dataset.i18n);
  }
  for (const el of document.querySelectorAll("[data-i18n-placeholder]")) {
    const [key, substitution] = el.dataset.i18nPlaceholder.split("|");
    el.placeholder = t(key, substitution);
  }
}
