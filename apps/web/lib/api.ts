export type Data = Record<string, any>;
export async function api<T=Data>(path:string, body?:unknown,token?:string):Promise<T> {
 const res=await fetch('/api'+path,{method:body===undefined?'GET':'POST',headers:{...(body instanceof FormData?{}:{'Content-Type':'application/json'}),...(token?{'X-Booking-Token':token}:{})},body:body===undefined?undefined:body instanceof FormData?body:JSON.stringify(body),credentials:'same-origin',cache:'no-store'});
 if(!res.ok){const e=await res.json().catch(()=>({error:'server_error'}));throw new Error(e.error||'server_error');} if(res.status===204)return {} as T;return res.json();
}
