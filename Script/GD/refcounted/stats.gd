extends RefCounted
class_name Stats

var buffs: Array[BuffInstance] = []


func add_buff(buff_resource: Buff, stacks: int = 1) -> void:
    if buff_resource.stackable:
        for bi in buffs:
            if bi.buff != null and bi.buff.id == buff_resource.id:
                bi.add_stacks(stacks)
                bi.remaining_duration = maxi(bi.remaining_duration, buff_resource.duration)
                buff_resource.on_apply(self, bi.stacks)
                return

    var instance := BuffInstance.new(buff_resource, stacks)
    buffs.append(instance)
    buff_resource.on_apply(self, stacks)


func remove_buff(buff_id: String) -> void:
    for i in range(buffs.size() - 1, -1, -1):
        var bi := buffs[i]
        if bi != null and bi.buff != null and bi.buff.id == buff_id:
            bi.buff.on_remove(self)
            buffs.remove_at(i)


func has_buff(buff_id: String) -> bool:
    for bi in buffs:
        if bi != null and bi.buff != null and bi.buff.id == buff_id:
            return true
    return false


func get_buff_stacks(buff_id: String) -> int:
    for bi in buffs:
        if bi != null and bi.buff != null and bi.buff.id == buff_id:
            return bi.stacks
    return 0


func tick_turn_start() -> void:
    var expired: Array[BuffInstance] = []
    for bi in buffs:
        if bi == null or bi.buff == null:
            continue
        bi.buff.on_turn_start(self, bi.stacks)
        if bi.tick_duration():
            bi.buff.on_remove(self)
            expired.append(bi)
    for bi in expired:
        buffs.erase(bi)


func tick_turn_end() -> void:
    var expired: Array[BuffInstance] = []
    for bi in buffs:
        if bi == null or bi.buff == null:
            continue
        bi.buff.on_turn_end(self, bi.stacks)
        if bi.tick_duration():
            bi.buff.on_remove(self)
            expired.append(bi)
    for bi in expired:
        buffs.erase(bi)


func clear_buffs() -> void:
    for bi in buffs:
        if bi != null and bi.buff != null:
            bi.buff.on_remove(self)
    buffs.clear()