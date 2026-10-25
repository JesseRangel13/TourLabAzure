import { expect, Page } from '@playwright/test';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Persist only request timestamps, never cookies, users, passwords or browser state.
// Playwright may recycle workers between files, so a module-local counter is insufficient.
export async function credentialTurn(): Promise<void> {
  const report=process.env['AZURELAB_BROWSER_REPORT'];
  if(!report)throw new Error('Use the private full-stack evidence directory.');
  mkdirSync(report,{recursive:true});
  const file=resolve(report,'credential-times.json');
  const saved:unknown=existsSync(file)?JSON.parse(readFileSync(file,'utf8')):[];
  if(!Array.isArray(saved) || saved.some(value=>!Number.isSafeInteger(value)))throw new Error('Invalid private pacing metadata.');
  const now=Date.now();
  const times=(saved as number[]).filter(value=>value>now-61_000 && value<=now);
  if(times.length>=9){
    await new Promise(resolve=>setTimeout(resolve,Math.max(1,times[0]+61_000-now)));
    return credentialTurn();
  }
  writeFileSync(file,JSON.stringify([...times,now]));
}
export async function login(page: Page, name: string) {
  const file = process.env['AZURELAB_IDENTITY_SEEDS'];
  if (!file) throw new Error('Use Verify-FullStack.ps1 with fresh private Azure lab users.');
  const user = (JSON.parse(readFileSync(file, 'utf8')).users as {userName:string;password:string}[])
    .find(value => value.userName === name)!;
  await page.goto('/login?returnUrl=/tours');
  await page.getByLabel('User name', {exact:true}).fill(user.userName);
  await page.getByLabel('Password', {exact:true}).fill(user.password);
  await credentialTurn();
  const pending=page.waitForResponse(response=>new URL(response.url()).pathname==='/auth/login' && response.request().method()==='POST');
  await page.getByRole('button', {name:'Sign in',exact:true}).click();
  expect((await pending).status(),'Real sign-in status').toBe(200);
  await expect(page.getByRole('heading', {name:'Tours',exact:true})).toBeVisible();
}
export async function logout(page: Page) {
  await credentialTurn();
  await page.getByRole('button', {name:'Sign out',exact:true}).click();
  await expect(page.getByRole('heading', {name:'Sign in',exact:true})).toBeVisible();
}
