import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { UserIcon } from "../../components/icons";
import { api, errorMessage } from "../../lib/api";
import { AuthCard } from "./AuthCard";
import { useAuthOptions } from "./useAuth";

interface ForgotPasswordFormProps {
  username: string;
  onUsernameChange: (value: string) => void;
  onBack: () => void;
}

export function ForgotPasswordForm({ username, onUsernameChange, onBack }: ForgotPasswordFormProps) {
  const options = useAuthOptions();
  const request = useMutation({ mutationFn: (name: string) => api.post<void>("/api/auth/password-reset", { username: name }) });
  const [submitted, setSubmitted] = useState(false);
  const usernameMissing = username.trim() === "";

  function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitted(true);
    if (!usernameMissing) request.mutate(username.trim());
  }

  return (
    <AuthCard title="Reset your password">
      {options.isPending && <div className="skeleton h-24 w-full" />}
      {options.data && !options.data.passwordReset && (
        <div role="status" className="alert alert-info alert-soft text-sm">
          <span>Email reset is not set up for this practice yet. Ask your practice manager to reset your password.</span>
        </div>
      )}
      {options.data?.passwordReset && request.isSuccess && (
        <div role="status" className="alert alert-success alert-soft text-sm">
          <span>
            If <strong>{username.trim()}</strong> has an email address on file, a reset link is on its way. It works once and expires soon.
          </span>
        </div>
      )}
      {options.data?.passwordReset && !request.isSuccess && (
        <form onSubmit={submit} noValidate className="space-y-4">
          <p className="text-sm text-base-content/70">Enter your username and we will email you a link to choose a new password.</p>
          <div className="fieldset p-0">
            <label htmlFor="reset-username" className="fieldset-legend pt-0">Username</label>
            <label className={`input w-full ${submitted && usernameMissing ? "input-error" : ""}`}>
              <UserIcon className="opacity-50" />
              <input id="reset-username" className="grow" value={username} onChange={(e) => onUsernameChange(e.target.value)} autoComplete="username" autoFocus />
            </label>
            {submitted && usernameMissing && <p className="label text-error">Enter your username.</p>}
          </div>
          {request.isError && (
            <div role="alert" className="alert alert-error alert-soft py-2 text-sm">
              <span>{errorMessage(request.error)}</span>
            </div>
          )}
          <button type="submit" className="btn btn-primary w-full" disabled={request.isPending}>
            {request.isPending && <span className="loading loading-spinner loading-sm" />}
            Email me a reset link
          </button>
        </form>
      )}
      <button type="button" className="btn btn-ghost btn-sm w-full" onClick={onBack}>Back to sign in</button>
    </AuthCard>
  );
}
