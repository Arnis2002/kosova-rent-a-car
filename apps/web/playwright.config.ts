import {defineConfig,devices} from '@playwright/test';
export default defineConfig({testDir:'./tests/browser',fullyParallel:false,workers:1,timeout:60000,use:{baseURL:'http://127.0.0.1:3000',headless:true,channel:process.env.CI?undefined:'chrome',screenshot:'only-on-failure',trace:'retain-on-failure'},reporter:[['list'],['html',{outputFolder:'../../artifacts/playwright',open:'never'}]],projects:[{name:'desktop',use:{...devices['Desktop Chrome']}}]});

