import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from "react";
import { appConfig } from "../lib/config";

type Tone = "success" | "error";

interface Toast {
  id: number;
  tone: Tone;
  message: string;
}

const ToastContext = createContext<(tone: Tone, message: string) => void>(() => undefined);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const show = useCallback((tone: Tone, message: string) => {
    const id = ++nextId.current;
    setToasts((current) => [...current, { id, tone, message }]);
    window.setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), appConfig.toastMs);
  }, []);

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="toast toast-end z-50" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`alert ${toast.tone === "success" ? "alert-success" : "alert-error"} shadow-lg`}>
            <span>{toast.message}</span>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
