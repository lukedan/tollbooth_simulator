import sys

def collect(input, output):
	car_spawn = {}
	car_time = {}
	car_from = {}
	car_to = {}
	car_dist = {}
	ttot = 0.0
	totaltime = 0.0
	with open(input, 'r') as fin:
		for line in fin.readlines():
			sv = line.split()
			time = float(sv[0])
			carid = int(sv[2])
			if sv[1] == 's': # spawning
				car_spawn[carid] = time
				car_from[carid] = int(sv[3])
			elif sv[1] == 'c': # change of lane
				pass
			elif sv[1] == 'd': # disposal
				if car_spawn[carid] > totaltime:
					totaltime = car_spawn[carid]
				car_time[carid] = time - car_spawn[carid]
				ttot += car_time[carid]
				car_to[carid] = int(sv[3])
				car_dist[carid] = float(sv[4])
	totnum = len(car_to)
	average = ttot / totnum
	tsqd = 0.0
	for x in car_to.keys():
		tmp = car_time[x] - average
		tsqd += tmp * tmp
	tsqd /= totnum
	with open(output, 'w') as fout:
		for x in car_to.keys():
			fout.write('{0} {1} {2} {3} {4} {5}\n'.format(x, car_time[x], car_dist[x], car_from[x], car_to[x], car_dist[x] / car_time[x]))
	return (totaltime, totnum, totnum / totaltime, average, tsqd), (car_spawn, car_time, car_from, car_to, car_dist), car_to.keys()

def main():
	# res = []
	# for x in range(-3, 10, 1):
	# 	res.append(collect(str(x) + '/events.txt', str(x) + '/cardata.txt')[0])
	# with open('concl.txt', 'w') as fout:
	# 	for i in range(5):
	# 		for j in res:
	# 			fout.write(str(j[i]) + ' ')
	# 		fout.write('\n')
	stats, data, ids = collect('events.txt', 'cardata.txt')
	exitstat = {}
	for k, v in data[3].items():
		if v not in exitstat.keys():
			exitstat[v] = 0
		exitstat[v] += 1
	tot = 0
	for x in exitstat.values():
		tot += x
	for k, v in exitstat.items():
		print k, '=', v / float(tot)

if __name__ == '__main__':
	main()
