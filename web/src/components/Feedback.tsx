import { errorMessage } from "../lib/api";

export function ErrorAlert({ error }: { error: unknown }) {
  return (
    <div role="alert" className="alert alert-error alert-soft">
      <span>{errorMessage(error)}</span>
    </div>
  );
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="px-4 py-12 text-center">
      <p className="font-medium">{title}</p>
      {hint && <p className="mt-1 text-sm text-base-content/60">{hint}</p>}
    </div>
  );
}
