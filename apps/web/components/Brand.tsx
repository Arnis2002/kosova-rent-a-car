import Image from 'next/image';

export default function Brand({inverse=false,compact=false}:{inverse?:boolean;compact?:boolean}){
 return <span className={`brand-lockup${inverse?' brand-lockup-inverse':''}${compact?' brand-lockup-compact':''}`} aria-label="Rent Kosova">
  <Image className="brand-symbol" src="/brand/rent-kosova-mark.svg" width={44} height={44} alt="" priority/>
  {!compact&&<span className="brand-name"><strong>Rent</strong><span>Kosova</span></span>}
 </span>;
}
