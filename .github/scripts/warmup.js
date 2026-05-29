const { chromium } = require('playwright');

const FRONTEND = 'https://teddyyee-coffee-shop-frontend.vercel.app/warming-up';
const PING_URL = 'https://dotnetcoffeeshopbackend.onrender.com/ping';
const TIMEOUT = 10 * 60 * 1000;

async function isWarm() {
  try {
    const res = await fetch(PING_URL);
    return res.status === 200;
  } catch {
    return false;
  }
}

(async () => {
  if (await isWarm()) {
    console.log('Backend already warm');
    process.exit(0);
  }

  console.log('Backend cold — visiting frontend to trigger warm-up');
  const browser = await chromium.launch();
  const page = await browser.newPage();

  const warm = page.waitForResponse(
    r => r.url().startsWith(PING_URL) && r.status() === 200,
    { timeout: TIMEOUT }
  );

  await page.goto(FRONTEND, { waitUntil: 'domcontentloaded', timeout: 60_000 });
  console.log('Waiting for /ping to return 200...');
  await warm;

  console.log('Backend is warm');
  await browser.close();
})().catch(err => {
  console.error('Warmup failed:', err.message);
  process.exit(1);
});
