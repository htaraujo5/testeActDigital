export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`skeleton ${className}`.trim()} aria-hidden />
}

export function DashboardSkeleton() {
  return (
    <div className="stack gap-24">
      <Skeleton className="skeleton-hero" />
      <div className="quick-actions">
        <Skeleton className="skeleton-action" />
        <Skeleton className="skeleton-action" />
      </div>
      <Skeleton className="skeleton-list" />
    </div>
  )
}
