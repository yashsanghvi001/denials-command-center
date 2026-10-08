export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  // An expired session anywhere except the sign-in endpoints sends the user back to sign in with a clean cache.
  if (response.status === 401 && !url.startsWith("/api/auth/")) window.location.assign("/");
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { title?: string } | null;
    const message =
      response.status === 403
        ? "You don't have access to this. It may be assigned to someone else."
        : (problem?.title ?? `Request failed (${response.status}).`);
    throw new ApiError(response.status, message);
  }
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(url: string) => request<T>("GET", url),
  post: <T>(url: string, body?: unknown) => request<T>("POST", url, body),
  put: <T>(url: string, body?: unknown) => request<T>("PUT", url, body),
  patch: <T>(url: string, body?: unknown) => request<T>("PATCH", url, body),
};

export async function orNull<T>(request: Promise<T>): Promise<T | null> {
  try {
    return await request;
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export const errorMessage = (error: unknown) => (error instanceof Error ? error.message : "Something went wrong. Try again.");
