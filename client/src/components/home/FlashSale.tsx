import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowRight, Zap } from "lucide-react";
import type { Product } from "../../types";

function useCountdownToEndOfDay() {
  const calc = () => {
    const now = new Date();
    const end = new Date(now);
    end.setHours(23, 59, 59, 999);
    const diff = Math.max(0, end.getTime() - now.getTime());
    return {
      h: Math.floor(diff / 3_600_000),
      m: Math.floor((diff % 3_600_000) / 60_000),
      s: Math.floor((diff % 60_000) / 1000),
    };
  };
  const [time, setTime] = useState(calc);
  useEffect(() => {
    const id = setInterval(() => setTime(calc()), 1000);
    return () => clearInterval(id);
  }, []);
  return time;
}

function pad(n: number) {
  return n.toString().padStart(2, "0");
}

function TimeBox({ value }: { value: number }) {
  return (
    <span className="grid place-items-center min-w-[2rem] h-8 px-1.5 rounded-lg bg-slate-900 dark:bg-white text-white dark:text-slate-900 font-mono font-bold text-sm tabular-nums">
      {pad(value)}
    </span>
  );
}

function FlashCard({ product }: { product: Product }) {
  // synthetic "original" price to show a discount badge
  const original = product.price * 1.4;
  const off = Math.round((1 - product.price / original) * 100);
  return (
    <Link to={`/products/${product.id}`} className="group shrink-0 w-40">
      <div className="relative aspect-square rounded-xl overflow-hidden bg-slate-100 dark:bg-slate-800">
        {product.imageUrl && (
          <img src={product.imageUrl} alt={product.name} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
        )}
        <span className="absolute top-1.5 left-1.5 rounded-md bg-rose-500 text-white text-[10px] font-bold px-1.5 py-0.5">-{off}%</span>
      </div>
      <div className="mt-1.5 text-sm line-clamp-1">{product.name}</div>
      <div className="flex items-baseline gap-1.5">
        <span className="font-bold text-rose-500">${product.price.toFixed(2)}</span>
        <span className="text-xs muted line-through">${original.toFixed(2)}</span>
      </div>
    </Link>
  );
}

export function FlashSale({ products }: { products: Product[] }) {
  const { h, m, s } = useCountdownToEndOfDay();
  if (products.length === 0) return null;
  return (
    <section className="rounded-3xl bg-gradient-to-br from-rose-50 to-orange-50 dark:from-rose-950/30 dark:to-orange-950/20 border border-rose-100 dark:border-rose-900/40 p-5">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
        <div className="flex items-center gap-2">
          <Zap className="w-5 h-5 text-rose-500 fill-rose-500" aria-hidden />
          <h2 className="text-lg font-bold">Flash Sale</h2>
          <div className="flex items-center gap-1" role="timer" aria-label={`Kết thúc sau ${h} giờ ${m} phút`}>
            <span className="text-xs muted mr-1">Kết thúc sau</span>
            <TimeBox value={h} /><span className="font-bold">:</span>
            <TimeBox value={m} /><span className="font-bold">:</span>
            <TimeBox value={s} />
          </div>
        </div>
        <Link to="/products" className="text-sm text-rose-500 hover:underline inline-flex items-center gap-1">
          Xem tất cả <ArrowRight className="w-4 h-4" aria-hidden />
        </Link>
      </div>
      <div className="flex gap-4 overflow-x-auto pb-1">
        {products.map((p) => <FlashCard key={p.id} product={p} />)}
      </div>
    </section>
  );
}
