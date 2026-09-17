import type { Metadata } from 'next';
import './globals.css';
import { Noto_Sans_Arabic, Plus_Jakarta_Sans } from 'next/font/google';
import { cn } from '@/lib/utils';
import { TooltipProvider } from '@/components/ui/tooltip';

/**
 * Task F1.2: self-hosted, not a runtime Google Fonts request - `next/font/google`
 * downloads the font files once at build time and serves them from this app's own
 * origin (no request to fonts.googleapis.com ever leaves the browser).
 *
 * Noto Sans Arabic is the product's ONLY prose font (docs/31: Arabic-only UI). Plus
 * Jakarta Sans is reserved for isolated Latin runs - item codes, document numbers,
 * mobile numbers - never for Arabic prose (see `.font-mono-code` in globals.css).
 */
const notoSansArabic = Noto_Sans_Arabic({
  subsets: ['arabic'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-arabic',
  display: 'swap',
});

const plusJakartaSans = Plus_Jakarta_Sans({
  subsets: ['latin'],
  weight: ['400', '600', '700'],
  variable: '--font-latin',
  display: 'swap',
});

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
    <html
      lang="ar"
      dir="rtl"
      className={cn('font-sans', notoSansArabic.variable, plusJakartaSans.variable)}
    >
      <body>
        <TooltipProvider>{children}</TooltipProvider>
      </body>
    </html>
  );
}
