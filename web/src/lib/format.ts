const currency = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

const calendarDate = new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric", timeZone: "UTC" });

export const formatMoney = (value: number) => currency.format(value);

export const formatDate = (value: string | null | undefined) => (value ? calendarDate.format(new Date(`${value}T00:00:00Z`)) : "Not set");

export const formatDateTime = (value: string) =>
  new Date(value).toLocaleString("en-US", { month: "short", day: "numeric", year: "numeric", hour: "numeric", minute: "2-digit" });

export const splitWords = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");

export function daysLeftText(days: number | null) {
  if (days === null) return "No deadline";
  if (days === -1) return "Passed 1 day ago";
  if (days < 0) return `Passed ${-days} days ago`;
  if (days === 0) return "Due today";
  return days === 1 ? "1 day left" : `${days} days left`;
}

const bucketTones: Record<string, string> = {
  Recoverable: "badge-success",
  MaybeEligibility: "badge-warning",
};

const severityTones: Record<string, string> = {
  Error: "badge-error",
  Warning: "badge-warning",
};

const claimStatusTones: Record<string, string> = {
  Paid: "badge-success",
  Denied: "badge-error",
  PartiallyDenied: "badge-warning",
};

// A soft badge with no tone takes the theme's text colour, so it stays readable in both the light and dark themes.
// The neutral tone must not be used here: the dark theme's neutral is near-black and disappears on a dark card.
const badge = (tone: string | undefined) => `badge badge-soft whitespace-nowrap ${tone ?? ""}`.trimEnd();

export const bucketBadge = (bucket: string) => badge(bucketTones[bucket]);

export const severityBadge = (severity: string) => badge(severityTones[severity] ?? "badge-info");

export const claimStatusBadge = (status: string) => badge(claimStatusTones[status]);

export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join("");
