export function FullPageLoader() {
  return (
    <div className="grid min-h-screen place-items-center bg-base-200">
      <span className="loading loading-spinner loading-lg text-primary" aria-label="Loading" />
    </div>
  );
}

export function StatsSkeleton({ count }: { count: number }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="skeleton h-24 w-full" />
      ))}
    </div>
  );
}

export function TableSkeletonRows({ rows, columns }: { rows: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, row) => (
        <tr key={row}>
          {Array.from({ length: columns }, (_, column) => (
            <td key={column}>
              <div className="skeleton h-4 w-full" />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}

export function DetailSkeleton({ lines }: { lines: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: lines }, (_, index) => (
        <div key={index} className="skeleton h-4" style={{ width: `${55 + ((index * 17) % 40)}%` }} />
      ))}
    </div>
  );
}

export function PageSkeleton() {
  return (
    <div className="space-y-5">
      <div className="skeleton h-8 w-64" />
      <StatsSkeleton count={4} />
      <div className="skeleton h-96 w-full" />
    </div>
  );
}
