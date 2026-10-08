import type { ReactNode } from "react";

interface SectionCardProps {
  title: string;
  actions?: ReactNode;
  flush?: boolean;
  children: ReactNode;
}

export function SectionCard({ title, actions, flush, children }: SectionCardProps) {
  return (
    <section className="card bg-base-100 shadow-sm">
      <div className="flex min-h-12 items-center justify-between gap-3 border-b border-base-200 px-5 py-2">
        <h2 className="font-semibold">{title}</h2>
        {actions}
      </div>
      {flush ? <div className="overflow-x-auto">{children}</div> : <div className="p-5">{children}</div>}
    </section>
  );
}
