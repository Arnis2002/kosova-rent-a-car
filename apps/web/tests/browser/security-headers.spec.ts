import {expect,test} from '@playwright/test';

test('development React debugging policy matches the runtime mode',async({page})=>{
 const response=await page.goto('/en');
 expect(response).not.toBeNull();
 const policy=response!.headers()['content-security-policy']||'';
 const allowsUnsafeEval=policy.includes("'unsafe-eval'");
 expect(allowsUnsafeEval).toBe(process.env.EXPECT_UNSAFE_EVAL==='true');
});
