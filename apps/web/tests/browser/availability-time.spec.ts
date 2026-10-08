import {test,expect} from '@playwright/test';
import {kosovoLocal,occupiesDay,shiftDay} from '../../lib/availability';

test('calendar uses Kosovo time across summer and winter clock changes',()=>{
 expect(kosovoLocal('2026-07-01T22:30:00Z')).toBe('2026-07-02T00:30');
 expect(kosovoLocal('2026-12-01T23:30:00Z')).toBe('2026-12-02T00:30');
 expect(shiftDay('2026-12-31',1)).toBe('2027-01-01');
 expect(shiftDay('2028-02-28',1)).toBe('2028-02-29');
});
test('calendar excludes the midnight release day but includes turnaround crossing midnight',()=>{
 const period={start:'2026-07-01T08:00:00Z',end:'2026-07-01T22:00:00Z'};
 expect(occupiesDay(period,'2026-07-01')).toBe(true);
 expect(occupiesDay(period,'2026-07-02')).toBe(false);
 expect(occupiesDay({...period,blocked_until:'2026-07-01T23:00:00Z'},'2026-07-02')).toBe(true);
});
test('calendar covers the 23-hour and 25-hour Kosovo days correctly',()=>{
 expect(occupiesDay({start:'2026-03-28T23:00:00Z',end:'2026-03-29T22:00:00Z'},'2026-03-29')).toBe(true);
 expect(occupiesDay({start:'2026-03-28T23:00:00Z',end:'2026-03-29T22:00:00Z'},'2026-03-30')).toBe(false);
 expect(occupiesDay({start:'2026-10-24T22:00:00Z',end:'2026-10-25T23:00:00Z'},'2026-10-25')).toBe(true);
 expect(occupiesDay({start:'2026-10-24T22:00:00Z',end:'2026-10-25T23:00:00Z'},'2026-10-26')).toBe(false);
});
