import type { ReactNode } from "react";

export function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <main className="flex min-h-screen items-center justify-center bg-base-200 p-4">
      <div className="card w-full max-w-sm bg-base-100 shadow-xl">
        <div className="card-body gap-5 p-8">
          <div className="text-center">
            <div className="mx-auto mb-3 grid size-12 place-items-center rounded-2xl bg-primary text-lg font-bold text-primary-content">DC</div>
            <h1 className="text-xl font-semibold">Denials Command Center</h1>
            <p className="text-sm text-base-content/60">Gulfview Physician Partners</p>
          </div>
          <h2 className="text-center font-medium">{title}</h2>
          {children}
        </div>
      </div>
    </main>
  );
}
