A simple Mini-Redis server I coded in a few hours.

Use at your own peril. No guarantees. 

Listens at port 5555 - can accept multiple connections. 

Some implemented operations:
1.	SET key value
2.	SET key value EX seconds (need not implement other SET options)
3.	GET key
4.	DEL key
5.	DBSIZE
6.	INCR key
7.	ZADD key score member
8.	ZCARD key

The first 6 use a 10 MB memory bound MemoryCache.
The other 2 use a Simple Priority Queue for each Key (https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp)

Some guarantees I tried to put in:
Atomicity

Assumes:
1.	Keys and values can only be the characters from the set [a-zA-Z0-9-_].
2.	Commands are ASCII strings with space-delimited parameters.
3.	Responses are ASCII strings with space-delimited values (where appropriate).

Multiple connection support from different clients

Connect using telnet to test it out:
telnet localhost 5555
SET mykey cool-value
OK
GET mykey
cool-value
DEL mykey
OK
GET mykey
(nil)

