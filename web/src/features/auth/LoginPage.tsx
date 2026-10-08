import { useState, type FormEvent } from "react";
import { UserIcon } from "../../components/icons";
import { errorMessage } from "../../lib/api";
import { AuthCard } from "./AuthCard";
import { ForgotPasswordForm } from "./ForgotPasswordForm";
import { PasswordInput } from "./PasswordInput";
import { useLogin } from "./useAuth";

export function LoginPage() {
  const [mode, setMode] = useState<"signIn" | "forgot">("signIn");
  const [username, setUsername] = useState("");

  return mode === "signIn" ? (
    <SignInForm username={username} onUsernameChange={setUsername} onForgot={() => setMode("forgot")} />
  ) : (
    <ForgotPasswordForm username={username} onUsernameChange={setUsername} onBack={() => setMode("signIn")} />
  );
}

interface SignInFormProps {
  username: string;
  onUsernameChange: (value: string) => void;
  onForgot: () => void;
}

function SignInForm({ username, onUsernameChange, onForgot }: SignInFormProps) {
  const login = useLogin();
  const [password, setPassword] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const usernameMissing = username.trim() === "";
  const passwordMissing = password === "";

  function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitted(true);
    if (usernameMissing || passwordMissing) return;
    login.mutate({ username: username.trim(), password });
  }

  return (
    <AuthCard title="Sign in to continue">
      <form onSubmit={submit} noValidate className="space-y-4">
        <div className="fieldset p-0">
          <label htmlFor="username" className="fieldset-legend pt-0">Username</label>
          <label className={`input w-full ${submitted && usernameMissing ? "input-error" : ""}`}>
            <UserIcon className="opacity-50" />
            <input
              id="username"
              className="grow"
              value={username}
              onChange={(e) => onUsernameChange(e.target.value)}
              autoComplete="username"
              autoFocus
              aria-invalid={(submitted && usernameMissing) || undefined}
            />
          </label>
          {submitted && usernameMissing && <p className="label text-error">Enter your username.</p>}
        </div>
        <div className="fieldset p-0">
          <div className="flex items-center justify-between">
            <label htmlFor="password" className="fieldset-legend pt-0">Password</label>
            <button type="button" className="link link-primary text-xs" onClick={onForgot}>Forgot password?</button>
          </div>
          <PasswordInput id="password" value={password} onChange={setPassword} autoComplete="current-password" invalid={submitted && passwordMissing} />
          {submitted && passwordMissing && <p className="label text-error">Enter your password.</p>}
        </div>
        {login.isError && (
          <div role="alert" className="alert alert-error alert-soft py-2 text-sm">
            <span>{errorMessage(login.error)}</span>
          </div>
        )}
        <button type="submit" className="btn btn-primary w-full" disabled={login.isPending}>
          {login.isPending && <span className="loading loading-spinner loading-sm" />}
          {login.isPending ? "Signing in" : "Sign in"}
        </button>
      </form>
    </AuthCard>
  );
}
