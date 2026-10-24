import { expect, test, Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
test.use({ trace:'off', screenshot:'off' });
test.setTimeout(60_000);
async function login(page:Page,name:string){
 const path=process.env['AZURELAB_IDENTITY_SEEDS'];
 if(!path)throw new Error('Use the initialized Azure recovery profile.');
 const users=JSON.parse(readFileSync(path,'utf8')).users as {userName:string;password:string}[];
 const user=users.find(x=>x.userName===name)!;
 await page.goto('/login?returnUrl=/tours');await page.getByLabel('User name',{exact:true}).fill(user.userName);await page.getByLabel('Password',{exact:true}).fill(user.password);
 await page.getByRole('button',{name:'Sign in',exact:true}).click();await expect(page.getByRole('heading',{name:'Tours',exact:true})).toBeVisible();
}
test('Admin uses bounded simulations, diagnostics and DLQ peek while Bob cannot enter Admin pages',async({page,browser})=>{
 await login(page,'Admin');await page.getByRole('link',{name:'Simulations',exact:true}).click();
 await page.getByLabel('Service',{exact:true}).selectOption('payments');
 await page.getByLabel('Occurrences',{exact:true}).fill('101');
 await expect(page.getByRole('button',{name:'Apply simulated mode',exact:true})).toBeDisabled();
 await page.getByLabel('Occurrences',{exact:true}).fill('2');
 // Success is intentionally non-disruptive to concurrent synthetic booking browser cases.
 await page.getByLabel('Fault mode',{exact:true}).selectOption('Success');
 await page.getByRole('button',{name:'Apply simulated mode',exact:true}).click();
 await expect(page.getByRole('status')).toContainText('saved');
 await page.getByRole('button',{name:'Reset modes',exact:true}).click();
 await expect(page.getByText('Current: Success · remaining 0',{exact:true})).toBeVisible();
 await page.getByRole('link',{name:'Operations',exact:true}).click();
 await expect(page.getByText(/Pending Outbox:/).first()).toBeVisible();
 await page.getByRole('button',{name:'Peek first page',exact:true}).click();
 await expect(page.getByText('Peek does not lock or remove messages.',{exact:false})).toBeVisible();
 await page.getByRole('link',{name:'Sagas',exact:true}).click();
 await page.getByLabel('State filter',{exact:true}).fill('ManualReview');
 await page.getByRole('button',{name:'Filter sagas',exact:true}).click();
 const other=await browser.newContext({ignoreHTTPSErrors:false});
 try{
  const bob=await other.newPage();await login(bob,'Bob');await bob.goto('/simulations');
  await expect(bob.getByRole('heading',{name:'Tours',exact:true})).toBeVisible();
  await expect(bob.getByRole('link',{name:'Operations',exact:true})).toHaveCount(0);
 }finally{await other.close();}
});
