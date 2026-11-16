namespace ElementMage
{
    public enum Element { None, Fire, Water }

    public enum SkillKind { Normal, Ultimate }

    public enum SkillId
    {
        Fireball, FlameSword, FlameRing, Meteor,
        WaterSwarm, WaterJet, Torrent, Rain,
        FirePillar, AbyssVortex
    }

    public enum PassiveType { Area, Damage, Cooldown, MoveSpeed }

    public enum GameState { ElementSelect, Playing, LevelUp, Victory, Defeat }
}
