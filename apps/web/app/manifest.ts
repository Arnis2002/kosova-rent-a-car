import type {MetadataRoute} from 'next';

export default function manifest():MetadataRoute.Manifest{return {
 name:'Rent Kosova',short_name:'Rent Kosova',description:'Clear choices for the road ahead.',start_url:'/en',display:'standalone',background_color:'#F7F8F5',theme_color:'#2457D6',
 icons:[{src:'/icon.svg',sizes:'any',type:'image/svg+xml'}]
};}
