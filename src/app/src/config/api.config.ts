// API base URL: empty string in dev (Vite proxy handles /api/*),
// set VITE_API_BASE_URL to the Container App URL in production.
export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '';
