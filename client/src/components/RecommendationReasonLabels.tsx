import type { RecommendationReason } from "../types";

export function RecommendationReasonLabels({ reasons }: { reasons: RecommendationReason[] }) {
  if (reasons.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-1.5 px-1 pt-2" aria-label="Lý do gợi ý">
      {reasons.map((reason) => (
        <span
          key={reason.code}
          title={`Mã lý do: ${reason.code}`}
          data-reason-code={reason.code}
          className="rounded-full bg-indigo-50 px-2 py-1 text-[10px] leading-tight text-indigo-700 dark:bg-indigo-950/70 dark:text-indigo-300"
        >
          {reason.label}
        </span>
      ))}
    </div>
  );
}
