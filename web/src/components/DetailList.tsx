import type { ReactNode } from "react";

export function DetailList({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-[minmax(9rem,max-content)_1fr]">{children}</dl>;
}

export function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-sm text-base-content/60">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </>
  );
}
