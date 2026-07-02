import { create } from "zustand";
import { persist } from "zustand/middleware";

type Theme = "light" | "dark";

interface ThemeState {
  theme: Theme;
  toggle: () => void;
  apply: () => void;
}

function setHtmlClass(theme: Theme) {
  const root = document.documentElement;
  root.classList.toggle("dark", theme === "dark");
}

export const useTheme = create<ThemeState>()(
  persist(
    (set, get) => ({
      theme: "light",
      toggle: () => {
        const next: Theme = get().theme === "dark" ? "light" : "dark";
        setHtmlClass(next);
        set({ theme: next });
      },
      apply: () => setHtmlClass(get().theme),
    }),
    { name: "ecommerce-theme" }
  )
);
