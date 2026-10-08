import type { Metadata } from 'next';
import './globals.css';
import './brand.css';
import './availability.css';
export const metadata: Metadata = {applicationName:'Rent Kosova',title:{default:'Rent Kosova | Clear choices for the road ahead',template:'%s | Rent Kosova'},description:'Compare exact rental vehicles, total prices and conditions from professional rental companies in Kosovo.',icons:{icon:'/icon.svg'},openGraph:{title:'Rent Kosova',description:'Clear choices for the road ahead.',type:'website',locale:'en_US',siteName:'Rent Kosova'},robots:{index:false,follow:false}};
export default function RootLayout({children}:{children:React.ReactNode}) { return <html lang="en"><body>{children}</body></html>; }
