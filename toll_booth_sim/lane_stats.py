def parse_lane_usage(eventsfn, posfn):
	def update_car_table(curt, tart, lines, lineid, table):
		while curt < tart:
			while lines[lineid][0] != '#':
				lineid += 1
			curt = float(lines[lineid].split()[1])
			lineid += 1
		while lines[lineid][0] != '#':
			sv = lines[lineid].split()
			carid = int(sv[0])
			cardist = float(sv[4])
			table[carid] = cardist
			lineid += 1
		return lineid, curt

	curdist = {}
	lastdist = {}
	lasttime = {}
	lastnode = {}
	llnode = {}

	lanehits = {}
	lanetot_dist = {}
	lanetot_time = {}
	with open(posfn, 'r') as fin:
		poslines = fin.readlines()
	cur_time = -1.0;
	line_id = 0
	with open(eventsfn, 'r') as fin:
		for x in fin.readlines():
			sv = x.split()
			time = float(sv[0])
			line_id, cur_time = update_car_table(cur_time, time, poslines, line_id, curdist)
			etype = sv[1]
			carid = int(sv[2])
			if etype == 's':
				lastdist[carid] = 0
				llnode[carid] = int(sv[3])
				lastnode[carid] = int(sv[4])
				lasttime[carid] = cur_time
			else:
				clane = (llnode[carid], lastnode[carid])
				# print 'car', carid, clane, 'dist:', curdist[carid], lastdist[carid], curdist[carid] - lastdist[carid], 'time:', cur_time - lasttime[carid]
				if clane not in lanehits.keys():
					lanehits[clane] = 0
					lanetot_dist[clane] = 0
					lanetot_time[clane] = 0
				lanehits[clane] += 1
				lanetot_dist[clane] += curdist[carid] - lastdist[carid]
				lanetot_time[clane] += cur_time - lasttime[carid]
				if etype == 'c':
					lastdist[carid] = curdist[carid]
					llnode[carid] = lastnode[carid]
					lastnode[carid] = int(sv[3])
					lasttime[carid] = cur_time
	print '{0:>10}{1:>10}{2:>10}{3:>10}{4:>10}'.format('lane', 'hits', 'length', 'time', 'speed')
	for x in lanehits.keys():
		print '{0:>10}{1:10}{2:10.2f}{3:10.2f}{4:10.2f}'.format(
			x, lanehits[x], lanetot_dist[x] / lanehits[x],
			lanetot_time[x] / lanehits[x], lanetot_dist[x] / lanetot_time[x])
		# print x, 'dist:', lanetot_dist[x] / lanehits[x], 'time:', lanetot_time[x] / lanehits[x], 'spd', lanetot_dist[x] / lanetot_time[x]

def main():
	parse_lane_usage('events.txt', 'carpos.txt')

if __name__ == '__main__':
	main()
