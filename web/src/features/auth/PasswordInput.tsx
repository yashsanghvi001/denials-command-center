import { useState } from "react";
import { EyeIcon, EyeSlashIcon, LockIcon } from "../../components/icons";

interface PasswordInputProps {
  id: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete: "current-password" | "new-password";
  invalid?: boolean;
  describedBy?: string;
}

export function PasswordInput({ id, value, onChange, autoComplete, invalid, describedBy }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);

  return (
    <label className={`input w-full pr-1 ${invalid ? "input-error" : ""}`}>
      <LockIcon className="opacity-50" />
      <input
        id={id}
        type={visible ? "text" : "password"}
        className="grow"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        autoComplete={autoComplete}
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
      />
      <button
        type="button"
        className="btn btn-ghost btn-sm btn-square"
        onClick={() => setVisible((shown) => !shown)}
        aria-label={visible ? "Hide password" : "Show password"}
        aria-pressed={visible}
      >
        {visible ? <EyeSlashIcon /> : <EyeIcon />}
      </button>
    </label>
  );
}
