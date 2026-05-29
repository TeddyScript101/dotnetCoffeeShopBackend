const { chromium } = require('playwright');

const FRONTEND = 'https://teddyyee-coffee-shop-frontend.vercel.app';
const BACKEND = 'https://dotnetcoffeeshopbackend.onrender.com';
const TIMEOUT = 10 * 60 * 1000;

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  const warm = page.waitForResponse(
    r => r.url().startsWith(BACKEND + '/ping') && r.status() === 200,
    { timeout: TIMEOUT }
  );

  console.log(`Visiting ${FRONTEND}`);
  await page.goto(FRONTEND, { waitUntil: 'domcontentloaded', timeout: 60_000 });

  console.log('Waiting for backend /ping to return 200...');
  await warm;

  console.log('Backend is warm');
  await browser.close();
})().catch(err => {
  console.error('Warmup failed:', err.message);
  process.exit(1);
});
