import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'نظام إدارة المخازن',
  description: 'نظام إدارة المخزون والتوريد للمطاعم',
};

/**
 * Root layout.
 *
 * `dir="rtl"` and `lang="ar"` are set here and never overridden: the product is
 * Arabic-only and natively right-to-left (docs/31 section 4.3), not an LTR app with
 * a direction switch bolted on.
 */
export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ar" dir="rtl">
      <body>{children}</body>
    </html>
  );
}
