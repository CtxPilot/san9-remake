extends Control

var materials: Array[ShaderMaterial] = []
var poles: Array[Sprite2D] = []
var originals: Array[Sprite2D] = []
var direction_buttons: Array[Button] = []
var texture_cache := {}
var settings: Dictionary
var mesh: ArrayMesh
var font: Font
var status: Label
var pause_button: Button
var palette_picker: OptionButton
var auto_color_button: CheckButton
var fabric_picker: ColorPickerButton
var border_picker: ColorPickerButton
var ink_picker: OptionButton
var original_caption: Label
var palette_label: Label
var wind_label: Label
var elapsed := 0.0
var wind := 1.0
var paused := false
var direction := 1
var selected_palette := 0
var auto_color := false
var color_elapsed := 0.0
var custom_colors := false
var fabric_color: Color
var border_color: Color
var ink_policy := 0
const COLOR_INTERVAL := 2.0
var capture_dir := ""
var capture_scope := "all"


func _ready() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture="):
			capture_dir = argument.trim_prefix("--capture=")
		if argument.begins_with("--capture-scope="):
			capture_scope = argument.trim_prefix("--capture-scope=")
	RenderingServer.set_default_clear_color(Color("202b30"))
	font = FontFile.new()
	font.load_dynamic_font("res://assets/preview-font.ttf")
	settings = JSON.parse_string(FileAccess.get_file_as_string("res://assets/generated-material.json"))
	mesh = _make_mesh()
	_create_ui()
	_create_flags()
	_set_palette(0)
	_set_direction(1)
	_set_auto_color(capture_dir.is_empty())
	if not capture_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(capture_dir)
		_set_paused(true)
		set_process_input(false)
		if capture_scope == "custom":
			_capture_custom_only()
		else:
			_capture_sequence()


func _make_mesh() -> ArrayMesh:
	var vertices := PackedVector3Array()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	const COLUMNS := 64
	const ROWS := 80
	for y in range(ROWS + 1):
		for x in range(COLUMNS + 1):
			vertices.append(Vector3(x * 32.0 / COLUMNS, y * 40.0 / ROWS, 0))
			uvs.append(Vector2(x * 1.0 / COLUMNS, y * 1.0 / ROWS))
	for y in range(ROWS):
		for x in range(COLUMNS):
			var a := y * (COLUMNS + 1) + x
			indices.append_array(PackedInt32Array([a, a + 1, a + COLUMNS + 1, a + 1, a + COLUMNS + 2, a + COLUMNS + 1]))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var result := ArrayMesh.new()
	result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return result


func _filtered_texture(file: String) -> ImageTexture:
	if texture_cache.has(file):
		return texture_cache[file]
	var imported: Texture2D = load("res://assets/" + file)
	var image := imported.get_image()
	if image.is_compressed():
		image.decompress()
	image.fix_alpha_edges()
	image.generate_mipmaps()
	var texture := ImageTexture.create_from_image(image)
	texture_cache[file] = texture
	return texture


func _label(text: String, point: Vector2, size: int, color := Color("e7ebe5")) -> Label:
	var label := Label.new()
	label.text = text
	label.position = point
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	add_child(label)
	return label


func _button(text: String, point: Vector2, dimensions: Vector2) -> Button:
	var button := Button.new()
	button.text = text
	button.position = point
	button.size = dimensions
	button.add_theme_font_override("font", font)
	button.add_theme_font_size_override("font_size", 22)
	add_child(button)
	return button


func _create_ui() -> void:
	_label("旗帜对比  /  原版与最终方案", Vector2(52, 28), 34)
	_label("选择方向，两侧同步切换；大图与原尺寸同时显示", Vector2(54, 82), 20, Color("b0c0c5"))
	palette_label = _label("旗色", Vector2(1030, 45), 22)
	palette_picker = OptionButton.new()
	palette_picker.position = Vector2(1120, 34)
	palette_picker.size = Vector2(360, 46)
	palette_picker.add_theme_font_override("font", font)
	palette_picker.add_theme_font_size_override("font_size", 22)
	for entry in settings["palette"]:
		var colors: Array = entry["material_parameters"]
		var swatch := Image.create(24, 24, false, Image.FORMAT_RGB8)
		swatch.fill(Color(colors[0], colors[1], colors[2]))
		palette_picker.add_icon_item(ImageTexture.create_from_image(swatch), entry["label"])
	palette_picker.item_selected.connect(_choose_palette)
	add_child(palette_picker)
	auto_color_button = CheckButton.new()
	auto_color_button.text = "自动换色（每 2 秒）"
	auto_color_button.position = Vector2(1120, 82)
	auto_color_button.add_theme_font_override("font", font)
	auto_color_button.add_theme_font_size_override("font_size", 20)
	auto_color_button.toggled.connect(_set_auto_color)
	add_child(auto_color_button)
	_label("方向", Vector2(54, 145), 23)
	var group := ButtonGroup.new()
	for index in range(settings["directions"].size()):
		var button := _button("方向 %d" % index, Vector2(190 + index * 165, 132), Vector2(148, 48))
		button.toggle_mode = true
		button.button_group = group
		button.pressed.connect(_set_direction.bind(index))
		direction_buttons.append(button)
	_label("原版", Vector2(385, 224), 32)
	original_caption = _label("原始图集 · 四帧动画", Vector2(275, 273), 20, Color("b0c0c5"))
	_label("最终方案", Vector2(1090, 224), 32)
	_label("共享高清底材 · 连续动画", Vector2(1030, 273), 20, Color("b0c0c5"))
	var divider := ColorRect.new()
	divider.color = Color("405056")
	divider.position = Vector2(800, 220)
	divider.size = Vector2(1, 600)
	add_child(divider)
	_label("原尺寸", Vector2(385, 732), 20, Color("b0c0c5"))
	_label("原尺寸", Vector2(1125, 732), 20, Color("b0c0c5"))
	status = _label("", Vector2(54, 832), 21, Color("b0c0c5"))
	pause_button = _button("暂停", Vector2(54, 882), Vector2(150, 46))
	pause_button.pressed.connect(func(): _set_paused(not paused))
	_label("风幅（最终方案）", Vector2(275, 891), 21)
	var slider := HSlider.new()
	slider.position = Vector2(485, 889)
	slider.size = Vector2(290, 32)
	slider.min_value = 0
	slider.max_value = 2
	slider.step = 0.01
	slider.value = 1
	slider.value_changed.connect(func(value): wind = value)
	add_child(slider)
	wind_label = _label("1.0×", Vector2(800, 891), 22, Color("b0c0c5"))
	_label("布面", Vector2(900, 891), 20)
	fabric_picker = _color_picker(Vector2(960, 882))
	fabric_picker.color_changed.connect(func(value): fabric_color = value; _customize_colors())
	_label("边饰", Vector2(1090, 891), 20)
	border_picker = _color_picker(Vector2(1150, 882))
	border_picker.color_changed.connect(func(value): border_color = value; _customize_colors())
	_label("字色", Vector2(1280, 891), 20)
	ink_picker = OptionButton.new()
	ink_picker.position = Vector2(1340, 882)
	ink_picker.size = Vector2(190, 46)
	ink_picker.add_theme_font_override("font", font)
	ink_picker.add_theme_font_size_override("font_size", 20)
	for choice in ["自动", "黑字", "白字", "无字"]:
		ink_picker.add_item(choice)
	ink_picker.item_selected.connect(func(value): ink_policy = value; _customize_colors())
	add_child(ink_picker)
	_label("0–7 / ← → 切换方向    空格 暂停    G 网格    Esc 退出", Vector2(54, 952), 20, Color("b0c0c5"))
	_label("点击色块可试任意新配色", Vector2(1060, 952), 20, Color("b0c0c5"))


func _color_picker(point: Vector2) -> ColorPickerButton:
	var picker := ColorPickerButton.new()
	picker.position = point
	picker.size = Vector2(100, 46)
	picker.edit_alpha = false
	add_child(picker)
	return picker


func _create_flags() -> void:
	for small in [false, true]:
		var zoom := 1.0 if small else 8.0
		var y := 766.0 if small else 320.0
		var original := Sprite2D.new()
		original.centered = false
		original.region_enabled = true
		original.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		original.position = Vector2(430 - 33 * zoom, y)
		original.scale = Vector2.ONE * zoom
		add_child(original)
		originals.append(original)
		var root := Node2D.new()
		root.position = Vector2(1170 - 33 * zoom, y)
		root.scale = Vector2.ONE * zoom
		add_child(root)
		var pole := Sprite2D.new()
		pole.texture = _filtered_texture("pole.png")
		pole.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		pole.centered = false
		pole.position = Vector2(31, 8)
		pole.scale = Vector2.ONE / 16.0
		root.add_child(pole)
		poles.append(pole)
		var cloth := MeshInstance2D.new()
		cloth.mesh = mesh
		cloth.texture = _filtered_texture("cloth-generated-v1.png")
		cloth.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		var material := ShaderMaterial.new()
		material.shader = load("res://Cloth.gdshader")
		material.set_shader_parameter("emblem", _filtered_texture("commander.png"))
		material.set_shader_parameter("diagnostic_grid", false)
		var motion: Dictionary = settings["motion"]
		material.set_shader_parameter("flutter_period", motion["period_seconds"])
		for parameter in motion["coefficients"]:
			var values: Array = motion["coefficients"][parameter]
			material.set_shader_parameter(parameter, Vector3(values[0], values[1], values[2]))
		material.set_shader_parameter("light_response", motion["light_response"])
		var region: Array = settings["art_region"]
		material.set_shader_parameter("art_region", Vector4(region[0], region[1], region[2], region[3]))
		cloth.material = material
		root.add_child(cloth)
		materials.append(material)


func _process(delta: float) -> void:
	_advance(delta)
	_apply_time()


func _advance(delta: float) -> void:
	if paused:
		return
	elapsed += delta
	if auto_color:
		color_elapsed += delta
		var steps := floori(color_elapsed / COLOR_INTERVAL)
		if steps > 0:
			color_elapsed = fmod(color_elapsed, COLOR_INTERVAL)
			_set_palette(selected_palette + steps)


func _apply_time() -> void:
	var phase := floori((elapsed + 0.0000001) / settings["original_frame_seconds"]) % 4
	wind_label.text = "%.1f×" % wind
	for original in originals:
		original.region_rect = Rect2(phase * 68, direction * 52, 68, 52)
	for material in materials:
		material.set_shader_parameter("clock_seconds", elapsed)
		material.set_shader_parameter("wind_strength", wind)


func _set_auto_color(value: bool) -> void:
	auto_color = value
	color_elapsed = 0
	auto_color_button.set_pressed_no_signal(value)


func _choose_palette(value: int) -> void:
	_set_auto_color(false)
	_set_palette(value)


func _set_palette(value: int) -> void:
	custom_colors = false
	selected_palette = posmod(value, settings["palette"].size())
	palette_picker.select(selected_palette)
	var entry: Dictionary = settings["palette"][selected_palette]
	var colors: Array = entry["material_parameters"]
	fabric_color = Color(colors[0], colors[1], colors[2])
	border_color = Color(colors[3], colors[4], colors[5])
	fabric_picker.color = fabric_color
	border_picker.color = border_color
	ink_policy = 0
	ink_picker.select(0)
	for original in originals:
		original.texture = load("res://assets/" + entry["original_atlas"])
	_apply_colors()
	_apply_time()


func _customize_colors() -> void:
	custom_colors = true
	_set_auto_color(false)
	_apply_colors()


func _effective_ink() -> int:
	if not custom_colors:
		return settings["palette"][selected_palette]["glyph_ink"]
	if ink_policy != 0:
		return 1 if ink_policy == 2 else 0
	var luminance := fabric_color.srgb_to_linear().get_luminance()
	return 0 if (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) else 1


func _apply_colors() -> void:
	for material in materials:
		material.set_shader_parameter("faction_color", fabric_color)
		material.set_shader_parameter("trim_color", border_color)
		material.set_shader_parameter("emblem", _filtered_texture("commander-white.png" if _effective_ink() == 1 else "commander.png"))
	original_caption.text = "原版旗色 %02d · 新配色参照" % selected_palette if custom_colors else "原始图集 · 四帧动画"
	palette_label.text = "参照色" if custom_colors else "旗色"
	_apply_emblem_visibility()


func _apply_emblem_visibility() -> void:
	var has_emblem: bool = ink_policy != 3 if custom_colors else settings["palette"][selected_palette]["has_emblem"]
	var visible_emblem: bool = settings["directions"][direction]["show_emblem"] and has_emblem
	for material in materials:
		material.set_shader_parameter("show_emblem", visible_emblem)


func _set_direction(value: int) -> void:
	direction = posmod(value, settings["directions"].size())
	var profile: Dictionary = settings["directions"][direction]
	for index in range(direction_buttons.size()):
		direction_buttons[index].set_pressed_no_signal(index == direction)
	for index in range(materials.size()):
		materials[index].set_shader_parameter("yaw_radians", profile["yaw_radians"])
		materials[index].set_shader_parameter("projection_scale", Vector2(profile["projection_scale"][0], profile["projection_scale"][1]))
		materials[index].set_shader_parameter("attachment", Vector2(profile["attachment"][0], profile["attachment"][1]))
		poles[index].z_index = 1 if profile["pole_front"] else 0
	status.text = "当前方向 %d / 共八向%s" % [direction, " · 侧向，旗字隐藏" if not profile["show_emblem"] else ""]
	_apply_emblem_visibility()
	_apply_time()


func _set_paused(value: bool) -> void:
	paused = value
	pause_button.text = "继续" if paused else "暂停"


func _input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	var focused := get_viewport().gui_get_focus_owner()
	if focused is LineEdit or focused is TextEdit:
		return
	if event.keycode >= KEY_0 and event.keycode <= KEY_7:
		_set_direction(event.keycode - KEY_0)
		get_viewport().set_input_as_handled()
		return
	match event.keycode:
		KEY_SPACE: _set_paused(not paused)
		KEY_LEFT: _set_direction(direction - 1)
		KEY_RIGHT, KEY_D: _set_direction(direction + 1)
		KEY_G:
			for material in materials:
				material.set_shader_parameter("diagnostic_grid", not material.get_shader_parameter("diagnostic_grid"))
		KEY_ESCAPE: get_tree().quit()
		_: return
	get_viewport().set_input_as_handled()


func _capture(name: String) -> void:
	_apply_time()
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(capture_dir.path_join(name + ".png"))


func _capture_sequence() -> void:
	var period: float = settings["motion"]["period_seconds"]
	for index in range(36):
		elapsed = index * period / 36.0
		await _capture("frame-%03d" % index)
	elapsed = 0.4
	await _capture("repeat-a")
	await _capture("repeat-b")
	wind = 0
	elapsed = 0.2
	await _capture("zero-wind-a")
	elapsed = 0.6
	await _capture("zero-wind-b")
	wind = 1
	elapsed = 0
	await _capture("cycle-start")
	elapsed = period
	await _capture("cycle-end")
	elapsed = settings["original_frame_seconds"] * 2
	var audit := []
	for index in range(direction_buttons.size()):
		var key := InputEventKey.new()
		key.pressed = true
		key.keycode = KEY_0 + index
		_input(key)
		assert(direction == index)
		direction_buttons[index].emit_signal("pressed")
		assert(direction == index and direction_buttons[index].button_pressed)
		await _capture("direction-%d" % index)
		audit.append({"direction": direction, "original_frame": 2, "show_emblem": materials[0].get_shader_parameter("show_emblem"), "pole_front": poles[0].z_index == 1})
	var file := FileAccess.open(capture_dir.path_join("direction-audit.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(audit, "  "))
	file.close()
	palette_picker.item_selected.emit(5)
	assert(selected_palette == 5 and palette_picker.selected == 5)
	for index in range(direction_buttons.size()):
		direction_buttons[index].emit_signal("pressed")
		await _capture("dark-direction-%d" % index)
	_set_palette(0)
	for view in [1, 3, 5, 7]:
		_set_direction(view)
		for strength in range(3):
			wind = [0.5, 1.0, 2.0][strength]
			for phase in range(12):
				elapsed = phase * period / 12.0
				_apply_time()
				await get_tree().process_frame
				await RenderingServer.frame_post_draw
				var image := get_viewport().get_texture().get_image()
				image.get_region(Rect2i(860, 310, 620, 405)).save_png(capture_dir.path_join("wind-%d-%d-%02d.png" % [view, strength, phase]))
	var motion_file := FileAccess.open(capture_dir.path_join("motion-settings.json"), FileAccess.WRITE)
	motion_file.store_string(JSON.stringify(settings["motion"], "  "))
	motion_file.close()
	for view in range(8):
		_set_direction(view)
		wind = 1
		for phase in range(4):
			elapsed = phase * settings["original_frame_seconds"]
			await _capture("alignment-%d-%d" % [view, phase])
	await _capture_palette()
	await _capture_custom()
	print("FLAG_DIRECTION_CAPTURE_OK frames=36 directions=8 renderer=", RenderingServer.get_current_rendering_method())
	get_tree().quit()


func _capture_palette() -> void:
	wind = 1
	elapsed = 0.284
	_set_direction(1)
	var audit := []
	for index in range(settings["palette"].size()):
		palette_picker.item_selected.emit(index)
		assert(selected_palette == index and palette_picker.selected == index)
		await _capture("color-%02d" % index)
		audit.append({"color": index, "glyph_ink": settings["palette"][index]["glyph_ink"], "show_emblem": materials[0].get_shader_parameter("show_emblem")})
	_set_auto_color(true)
	_set_paused(false)
	_advance(COLOR_INTERVAL + 0.01)
	assert(selected_palette == 0, "Automatic cycling must wrap after color 50")
	_set_paused(true)
	var stopped_time := elapsed
	_advance(COLOR_INTERVAL * 2)
	assert(selected_palette == 0 and elapsed == stopped_time, "Pause must stop both clocks")
	palette_picker.item_selected.emit(3)
	assert(selected_palette == 3 and not auto_color and not auto_color_button.button_pressed)
	var file := FileAccess.open(capture_dir.path_join("palette-audit.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify({"colors": audit, "automatic_wrap": true, "pause_stops_both_clocks": true, "manual_selection_stops_auto": true}, "  "))
	file.close()


func _capture_custom_only() -> void:
	await _capture_custom()
	get_tree().quit()


func _capture_custom() -> void:
	_set_paused(true)
	wind = 1
	elapsed = 0.284
	_choose_palette(0)
	_set_direction(0)
	var editor := LineEdit.new()
	editor.position = Vector2(0, -100)
	add_child(editor)
	editor.grab_focus()
	set_process_input(true)
	var digit := InputEventKey.new()
	digit.pressed = true
	digit.keycode = KEY_1
	digit.unicode = 49
	Input.parse_input_event(digit)
	await get_tree().process_frame
	assert(direction == 0 and editor.text == "1", "Typing a hex color must not trigger direction shortcuts")
	set_process_input(false)
	editor.queue_free()
	_set_direction(1)
	await _capture("custom-baseline")
	_set_auto_color(true)
	fabric_picker.color = Color("713ab9")
	fabric_picker.color_changed.emit(fabric_picker.color)
	border_picker.color = Color("77ddbb")
	border_picker.color_changed.emit(border_picker.color)
	assert(custom_colors and not auto_color and _effective_ink() == 1)
	for view in range(8):
		_set_direction(view)
		await _capture("custom-dark-%d" % view)
	_set_direction(1)
	fabric_picker.color = Color("ffc8a1")
	fabric_picker.color_changed.emit(fabric_picker.color)
	assert(_effective_ink() == 0)
	await _capture("custom-light")
	ink_picker.item_selected.emit(2)
	assert(_effective_ink() == 1)
	await _capture("custom-white")
	ink_picker.item_selected.emit(3)
	assert(not materials[0].get_shader_parameter("show_emblem"))
	await _capture("custom-no-emblem")
	_choose_palette(0)
	assert(not custom_colors)
	await _capture("custom-restored")
	print("FLAG_CUSTOM_CAPTURE_OK")
