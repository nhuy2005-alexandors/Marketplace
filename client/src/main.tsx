import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'
import { useTheme } from './store/theme'
import { queryClient } from './api/queryClient'

// Áp theme đã lưu trước khi render để tránh flash sáng/tối.
useTheme.getState().apply()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
