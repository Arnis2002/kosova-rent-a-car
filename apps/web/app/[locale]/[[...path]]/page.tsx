import Marketplace from '@/components/Marketplace';
import {notFound} from 'next/navigation';
export default async function Page({params}:{params:Promise<{locale:string,path?:string[]}>}) {const p=await params;if(!['en','sq','de'].includes(p.locale))notFound();return <Marketplace locale={p.locale as 'en'|'sq'|'de'} path={p.path||[]}/>;}
