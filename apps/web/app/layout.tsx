import type { Metadata } from 'next';
import './globals.css';
export const metadata: Metadata = {title:{default:'Kosova Rent-A-Car | Find your way around Kosovo',template:'%s | Kosova Rent-A-Car'},description:'Compare exact vehicles from professional rental businesses. Start with Prishtina and Prishtina International Airport.',robots:{index:false,follow:false}};
export default function RootLayout({children}:{children:React.ReactNode}) { return <html lang="en"><body>{children}</body></html>; }
