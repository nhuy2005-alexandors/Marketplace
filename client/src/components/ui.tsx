import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { Inbox, Loader2, Star, type LucideIcon } from "lucide-react";

type Variant = "primary" | "ghost" | "danger";

export function Button({
  variant = "primary", className = "", children, ...rest
}: { variant?: Variant } & ButtonHTMLAttributes<HTMLButtonElement>) {
  const cls = variant === "primary" ? "btn-primary" : variant === "danger" ? "btn-danger" : "btn-ghost";
  return <button className={`${cls} ${className}`} {...rest}>{children}</button>;
}

export function Card({ className = "", children }: { className?: string; children: ReactNode }) {
  return <div className={`surface rounded-2xl ${className}`}>{children}</div>;
}

export function Input({ className = "", ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={`input-base ${className}`} {...rest} />;
}

export function Textarea({ className = "", ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={`input-base ${className}`} {...rest} />;
}

export function Select({ className = "", children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={`input-base ${className}`} {...rest}>{children}</select>;
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-4 mb-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="muted text-sm mt-1">{subtitle}</p>}
      </div>
      {actions}
    </div>
  );
}

export function Spinner({ label = "Đang tải..." }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-16 muted">
      <Loader2 className="h-5 w-5 text-brand-500 animate-spin" aria-hidden />
      {label}
    </div>
  );
}

export function EmptyState({ icon: Icon = Inbox, title, hint }: { icon?: LucideIcon; title: string; hint?: string }) {
  return (
    <div className="text-center py-16">
      <div className="mx-auto mb-4 grid place-items-center w-16 h-16 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500">
        <Icon className="w-7 h-7" strokeWidth={1.75} aria-hidden />
      </div>
      <div className="font-medium">{title}</div>
      {hint && <div className="muted text-sm mt-1">{hint}</div>}
    </div>
  );
}

// Rating stars: full/half/empty + a11y label
export function Stars({ value, count, size = 14 }: { value: number; count?: number; size?: number }) {
  const rounded = Math.round(value * 2) / 2;
  return (
    <div className="flex items-center gap-1 text-xs" role="img" aria-label={`${value.toFixed(1)} trên 5 sao`}>
      <div className="flex items-center gap-0.5">
        {[1, 2, 3, 4, 5].map((i) => {
          const fill = rounded >= i ? 1 : rounded >= i - 0.5 ? 0.5 : 0;
          return (
            <span key={i} className="relative inline-block" style={{ width: size, height: size }} aria-hidden>
              <Star className="absolute inset-0 text-slate-300 dark:text-slate-600" style={{ width: size, height: size }} strokeWidth={1.5} />
              {fill > 0 && (
                <span className="absolute inset-0 overflow-hidden" style={{ width: `${fill * 100}%` }}>
                  <Star className="text-amber-400 fill-amber-400" style={{ width: size, height: size }} strokeWidth={1.5} />
                </span>
              )}
            </span>
          );
        })}
      </div>
      {count !== undefined && <span className="muted">({count})</span>}
    </div>
  );
}
