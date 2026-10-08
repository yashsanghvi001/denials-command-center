// Tunable client-side values in one place; server-side limits live in the API's appsettings.json.
export const appConfig = {
  freshness: {
    default: 30_000,
    work: 15_000,
    lists: 60_000,
    details: 60_000,
    reference: 60 * 60_000,
  },
  // pageSizes, maxNoteLength and passwordMinLength must stay within the API's Limits and Auth settings in appsettings.json.
  pageSizes: [25, 50, 100],
  searchDebounceMs: 300,
  toastMs: 4_000,
  passwordMinLength: 8,
  maxNoteLength: 2_000,
} as const;
