import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['.next/**', 'node_modules/**', 'playwright-report/**', 'test-results/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    languageOptions: {
      globals: { ...globals.browser, ...globals.node },
    },
  },
  {
    /*
     * The RTL guard is scoped to component source only. Applied repository-wide it
     * matches its own message strings in this file and the regex literals in the
     * tests that assert the same rule - a linter reporting itself is noise, not signal.
     */
    files: ['src/**/*.{ts,tsx}'],
    rules: {
      /*
       * RTL guard (docs/31 section 4.3, BR-31-8).
       *
       * Physical direction utilities silently break a right-to-left layout: `pl-4` is
       * padding on the *left* in both directions, so in RTL it lands on the wrong side.
       * The logical equivalents (`ps-`, `pe-`, `ms-`, `me-`, `text-start`, `text-end`)
       * follow the reading direction. This rule is what stops one careless `ml-2` from
       * shipping, because it looks correct to anyone reviewing in LTR.
       */
      'no-restricted-syntax': [
        'error',
        {
          selector:
            "Literal[value=/(^|\\s)-?(p|m)(l|r)-[\\w./[\\]-]+(\\s|$)/]",
          message:
            'Use logical spacing utilities (ps-/pe-/ms-/me-) instead of pl-/pr-/ml-/mr-. See docs/31 section 4.3.',
        },
        {
          selector: "Literal[value=/(^|\\s)text-(left|right)(\\s|$)/]",
          message:
            'Use text-start / text-end instead of text-left / text-right. See docs/31 section 4.3.',
        },
      ],
    },
  },
);
