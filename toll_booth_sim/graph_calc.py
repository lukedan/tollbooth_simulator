import sys

def get_graph_data(reader):
	in_edges = {}
	out_edges = {}
	node_types = {}
	node_coords = {}
	num_nodes = int(reader.readline())
	for i in range(num_nodes):
		sv = reader.readline().split()
		nodeid = int(sv[3])
		node_types[nodeid] = int(sv[2])
		node_coords[nodeid] = (float(sv[0]), float(sv[1]))
	num_lanes = int(reader.readline())
	for i in range(num_lanes):
		sv = reader.readline().split()
		fn = int(sv[0])
		tn = int(sv[1])
		if not fn in out_edges.keys():
			out_edges[fn] = []
		if not tn in in_edges.keys():
			in_edges[tn] = []
		in_edges[tn].append(fn)
		out_edges[fn].append(tn)
	return in_edges, out_edges, node_types, node_coords

def main():
	with open(sys.argv[1], 'r') as fin:
		ein, eout, types, coords = get_graph_data(fin)
	from_nodes = []
	node_poss = {}
	node_speed = {}
	for x in types.keys():
		if types[x] == 1:
			from_nodes.append(x)
			node_speed[x] = 0.0
	for x in from_nodes:
		print 'node at', coords[x], ' possibility:',
		node_poss[x] = input()
	layer = 0
	while len(from_nodes) > 0:
		next_layer = set()
		for x in from_nodes:
			if x in eout.keys():
				for y in eout[x]:
					next_layer.add(y)
		print 'layer', layer
		for n in next_layer:
			poss = 1.0
			for i in ein[n]:
				poss *= (1.0 - node_poss[i] / len(eout[i]))
			node_poss[n] = 1.0 - poss
			print ' ', n, '\t', node_poss[n]

		layer += 1
		from_nodes = list(next_layer)

if __name__ == '__main__':
	main()
