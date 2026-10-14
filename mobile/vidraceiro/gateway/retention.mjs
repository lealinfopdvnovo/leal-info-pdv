import {readdir,stat,rm} from 'node:fs/promises';
import {join} from 'node:path';
export function retentionHours(raw=24){const hours=Number(raw);if(!Number.isFinite(hours)||hours<1||hours>168)throw Error('RETENTION_HOURS deve estar entre 1 e 168.');return hours;}
// Only private generated job directories, never arbitrary files or PDV data.
export async function pruneJobs(directory,hours=24,now=Date.now()){
 const cutoff=now-retentionHours(hours)*3600000;let count=0;
 for(const entry of await readdir(directory,{withFileTypes:true})){
  if(!entry.isDirectory()||!/^[0-9a-f-]{36}$/i.test(entry.name))continue;
  const path=join(directory,entry.name);try{if((await stat(path)).mtimeMs<cutoff){await rm(path,{recursive:true,force:true});count++;}}catch(error){if(error.code!=='ENOENT')throw error;}
 }
 return count;
}
