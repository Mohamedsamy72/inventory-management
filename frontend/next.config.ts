import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // The API is a separate ASP.NET Core host (docs/19 section 1). The frontend never
  // talks to PostgreSQL directly — it goes through the API, always.
  env: {
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5165',
  },
};

export default nextConfig;
