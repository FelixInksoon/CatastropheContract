extends SceneTree

const OUTPUT_PATH := "E:/SteamLibrary/steamapps/common/Slay the Spire 2/mods/mods/CatastropheContract/CatastropheContract.pck"
const ROOT_PATH := "E:/SteamLibrary/steamapps/common/Slay the Spire 2/mods/mods/CatastropheContract"

var files_to_pack := [
	{
		"src": ROOT_PATH + "/godot/project.godot",
		"dest": "res://project.godot"
	},
	{
		"src": ROOT_PATH + "/godot/MainFile.cs",
		"dest": "res://MainFile.cs"
	},
	{
		"src": ROOT_PATH + "/godot/ui/CatastropheContractPanel.tscn",
		"dest": "res://mods/CatastropheContract/godot/ui/CatastropheContractPanel.tscn"
	},
	{
		"src": ROOT_PATH + "/godot/loc/catastrophe_contract.en.json",
		"dest": "res://mods/CatastropheContract/godot/loc/catastrophe_contract.en.json"
	},
	{
		"src": ROOT_PATH + "/godot/loc/catastrophe_contract.zh-CN.json",
		"dest": "res://mods/CatastropheContract/godot/loc/catastrophe_contract.zh-CN.json"
	},
	{
		"src": ROOT_PATH + "/godot/src/UI/ContractPanelNode.cs",
		"dest": "res://src/UI/ContractPanelNode.cs"
	}
]

func _init() -> void:
	var packer := PCKPacker.new()
	var open_result := packer.pck_start(OUTPUT_PATH)
	if open_result != OK:
		push_error("Failed to start pck: %s" % open_result)
		quit(1)
		return

	for entry in files_to_pack:
		var add_result := packer.add_file(entry.dest, entry.src)
		if add_result != OK:
			push_error("Failed to add file %s -> %s: %s" % [entry.src, entry.dest, add_result])
			quit(1)
			return

	var flush_result := packer.flush()
	if flush_result != OK:
		push_error("Failed to flush pck: %s" % flush_result)
		quit(1)
		return

	print("PCK built at %s" % OUTPUT_PATH)
	quit()
