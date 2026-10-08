import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, errorMessage } from "../../lib/api";
import { AuthCard } from "./AuthCard";
import { PasswordInput } from "./PasswordInput";
import { meetsPasswordRules, passwordRules } from "./passwordRules";

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const reset = useMutation({ mutationFn: () => api.post<void>("/api/auth/password-reset/confirm", { token, newPassword: password }) });
  const strongEnough = meetsPasswordRules(password);
  const matches = password === confirmation;

  function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitted(true);
    if (strongEnough && matches) reset.mutate();
  }

  if (!token) {
    return (
      <AuthCard title="Reset your password">
        <div role="alert" className="alert alert-warning alert-soft text-sm">
          <span>This reset link is incomplete. Open the link from the email again, or ask for a new one.</span>
        </div>
        <Link to="/" className="btn btn-ghost btn-sm w-full">Back to sign in</Link>
      </AuthCard>
    );
  }

  if (reset.isSuccess) {
    return (
      <AuthCard title="Password changed">
        <div role="status" className="alert alert-success alert-soft text-sm">
          <span>Your password has been changed. Sign in with your new password.</span>
        </div>
        <Link to="/" className="btn btn-primary w-full">Go to sign in</Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Choose a new password">
      <form onSubmit={submit} noValidate className="space-y-4">
        <div className="fieldset p-0">
          <label htmlFor="new-password" className="fieldset-legend pt-0">New password</label>
          <PasswordInput id="new-password" value={password} onChange={setPassword} autoComplete="new-password" invalid={submitted && !strongEnough} describedBy="password-rules" />
          <ul id="password-rules" className="mt-1 space-y-0.5 text-xs">
            {passwordRules.map((rule) => {
              const met = rule.test(password);
              return (
                <li key={rule.label} className={met ? "text-success" : submitted ? "text-error" : "text-base-content/60"}>
                  <span aria-hidden="true">{met ? "✓" : "•"}</span> {rule.label}
                </li>
              );
            })}
          </ul>
        </div>
        <div className="fieldset p-0">
          <label htmlFor="confirm-password" className="fieldset-legend pt-0">Confirm new password</label>
          <PasswordInput id="confirm-password" value={confirmation} onChange={setConfirmation} autoComplete="new-password" invalid={submitted && !matches} />
          {submitted && !matches && <p className="label text-error">The two passwords do not match.</p>}
        </div>
        {reset.isError && (
          <div role="alert" className="alert alert-error alert-soft py-2 text-sm">
            <span>{errorMessage(reset.error)}</span>
          </div>
        )}
        <button type="submit" className="btn btn-primary w-full" disabled={reset.isPending}>
          {reset.isPending && <span className="loading loading-spinner loading-sm" />}
          Save new password
        </button>
      </form>
      <Link to="/" className="btn btn-ghost btn-sm w-full">Back to sign in</Link>
    </AuthCard>
  );
}
