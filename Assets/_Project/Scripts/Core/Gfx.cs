using UnityEngine;

namespace ElementMage
{
    /// <summary>스프라이트 생성·크기 계산 등 비주얼 공용 함수</summary>
    public static class Gfx
    {
        // 정렬 순서
        public const int OrderGround = -100;
        public const int OrderZone = -10;
        public const int OrderOrb = 0;
        public const int OrderEnemy = 10;
        public const int OrderPlayer = 20;
        public const int OrderProjectile = 30;
        public const int OrderFx = 40;
        public const int OrderPopup = 60;

        public static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Material mat = GameManager.Db != null ? GameManager.Db.spriteMaterial : null;
            if (mat != null) sr.sharedMaterial = mat;
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        /// <summary>가산 혼합으로 그리는 스프라이트 (빛 번짐용)</summary>
        public static SpriteRenderer NewGlow(string name, Transform parent, Sprite sprite, int order, Color color)
        {
            SpriteRenderer sr = NewSprite(name, parent, sprite, order, color);
            Material mat = Vfx.Additive;
            if (mat != null) sr.sharedMaterial = mat;
            return sr;
        }

        /// <summary>스프라이트 가로 폭이 월드에서 width가 되도록 하는 균일 스케일</summary>
        public static float ScaleForWidth(Sprite s, float width)
        {
            if (s == null) return width;
            float w = s.bounds.size.x;
            return w > 0.0001f ? width / w : width;
        }

        /// <summary>스프라이트를 월드 크기 (width, height)로 늘리는 스케일</summary>
        public static Vector3 ScaleFor(Sprite s, float width, float height)
        {
            if (s == null) return new Vector3(width, height, 1f);
            Vector3 size = s.bounds.size;
            return new Vector3(size.x > 0.0001f ? width / size.x : width, size.y > 0.0001f ? height / size.y : height, 1f);
        }

        public static void SetDiameter(Transform t, Sprite s, float diameter)
        {
            float k = ScaleForWidth(s, diameter);
            t.localScale = new Vector3(k, k, 1f);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static float Angle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        public static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>카메라가 보는 영역의 절반 크기 (월드 단위)</summary>
        public static Vector2 ViewHalfExtents
        {
            get
            {
                Camera cam = GameManager.I != null ? GameManager.I.Cam : Camera.main;
                if (cam == null) return new Vector2(5f, 9f);
                return new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
            }
        }
    }

    /// <summary>한 번 재생되고 사라지는 스프라이트 이펙트 (크기·색 보간)</summary>
    public class SpriteFx : Pooled
    {
        SpriteRenderer sr;
        Transform tr;
        Vector3 scaleFrom, scaleTo;
        Color colorFrom, colorTo;
        float duration, time, spin;
        bool isAdditive;

        protected override void OnCreated()
        {
            tr = transform;
            sr = Gfx.NewSprite("Sprite", tr, null, Gfx.OrderFx, Color.white);
        }

        public static SpriteFx Play(Sprite sprite, Vector2 pos, float angle, Vector3 scaleFrom, Vector3 scaleTo,
            Color colorFrom, Color colorTo, float duration, int order = Gfx.OrderFx, float spin = 0f, bool additive = false)
        {
            SpriteFx fx = Pool<SpriteFx>.Get();
            fx.tr.position = pos;
            fx.tr.rotation = Quaternion.Euler(0f, 0f, angle);
            fx.sr.sprite = sprite;
            fx.sr.sortingOrder = order;
            if (fx.isAdditive != additive)
            {
                fx.isAdditive = additive;
                Material mat = additive ? Vfx.Additive : GameManager.Db.spriteMaterial;
                if (mat != null) fx.sr.sharedMaterial = mat;
            }
            fx.scaleFrom = scaleFrom;
            fx.scaleTo = scaleTo;
            fx.colorFrom = colorFrom;
            fx.colorTo = colorTo;
            fx.duration = Mathf.Max(0.01f, duration);
            fx.time = 0f;
            fx.spin = spin;
            fx.Apply(0f);
            return fx;
        }

        void Update()
        {
            time += Time.deltaTime;
            float t = time / duration;
            if (t >= 1f) { Release(); return; }
            Apply(t);
            if (spin != 0f) tr.Rotate(0f, 0f, spin * Time.deltaTime);
        }

        void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            tr.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, eased);
            sr.color = Color.LerpUnclamped(colorFrom, colorTo, t);
        }
    }

    /// <summary>자주 쓰는 이펙트 조합</summary>
    public static class Fx
    {
        /// <summary>
        /// 원형 폭발: 폭발 스프라이트 + 가산 섬광 + 충격파 고리 + 불티·불꽃 줄기 + 연기 + 바닥 그을음 + 화면 흔들림
        /// </summary>
        public static void Explosion(Vector2 pos, float radius, Color color)
        {
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            float d = radius * 2f;
            float k = Gfx.ScaleForWidth(db.burst, d);
            // 큰 폭발일수록 옅게 그려서 플레이어와 적이 가려지지 않게 한다
            float alpha = Mathf.Clamp(1.5f / Mathf.Max(0.1f, radius), 0.4f, 0.9f);
            Color hot = Color.Lerp(color, Color.white, 0.6f);

            SpriteFx.Play(db.burst, pos, Random.Range(0f, 360f), Vector3.one * k * 0.3f, Vector3.one * k * 1.12f,
                Gfx.WithAlpha(color, alpha), Gfx.WithAlpha(color, 0f), 0.34f);
            // 중심 섬광 (가산 혼합이라 겹칠수록 밝게 타오른다)
            Glow(pos, d * 1.5f, Gfx.WithAlpha(color, alpha * 0.55f), 0.26f);
            Glow(pos, d * 0.7f, Gfx.WithAlpha(hot, alpha * 0.9f), 0.16f);
            Shockwave(pos, radius * 1.15f, Gfx.WithAlpha(hot, 0.65f), 0.28f);

            int embers = Mathf.Clamp(Mathf.RoundToInt(5f + radius * 5f), 6, 18);
            Vfx.Burst(pos, color, embers, radius * 1.5f, radius * 4.5f, 0.1f, 0.26f, 0.3f, 0.65f);
            Vfx.Sparks(pos, hot, Mathf.Clamp(embers / 2, 4, 9), radius * 4f, radius * 9f);
            Color smoke = Color.Lerp(color, new Color(0.12f, 0.11f, 0.13f), 0.78f);
            Vfx.SmokePuff(pos, Gfx.WithAlpha(smoke, 0.34f), Mathf.Clamp(Mathf.RoundToInt(radius * 2.5f), 2, 6),
                radius * 0.6f, radius * 0.9f, 0.7f);
            if (radius >= 0.8f) Decal(pos, radius * 0.85f, new Color(0.03f, 0.03f, 0.05f, 0.3f), 1.6f);

            Juice.Shake(Mathf.Clamp(radius * 0.09f, 0.05f, 0.32f));
        }

        /// <summary>퍼져 나가는 고리 파동</summary>
        public static void Shockwave(Vector2 pos, float radius, Color color, float duration = 0.4f)
        {
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            float k = Gfx.ScaleForWidth(db.ring, radius * 2f);
            SpriteFx.Play(db.ring, pos, 0f, Vector3.one * k * 0.2f, Vector3.one * k,
                color, Gfx.WithAlpha(color, 0f), duration, Gfx.OrderFx - 1);
        }

        /// <summary>작은 타격 번쩍임</summary>
        public static void Puff(Vector2 pos, float size, Color color, float duration = 0.25f)
        {
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            float k = Gfx.ScaleForWidth(db.softCircle, size);
            SpriteFx.Play(db.softCircle, pos, 0f, Vector3.one * k * 0.4f, Vector3.one * k,
                color, Gfx.WithAlpha(color, 0f), duration);
        }

        /// <summary>가산 혼합 빛 번짐</summary>
        public static void Glow(Vector2 pos, float diameter, Color color, float duration = 0.25f)
        {
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            float k = Gfx.ScaleForWidth(db.softCircle, diameter);
            SpriteFx.Play(db.softCircle, pos, 0f, Vector3.one * k * 0.55f, Vector3.one * k,
                color, Gfx.WithAlpha(color, 0f), duration, Gfx.OrderFx + 1, 0f, true);
        }

        /// <summary>바닥에 잠시 남는 자국 (그을음·물웅덩이)</summary>
        public static void Decal(Vector2 pos, float radius, Color color, float duration)
        {
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            float k = Gfx.ScaleForWidth(db.softCircle, radius * 2f);
            SpriteFx.Play(db.softCircle, pos, 0f, Vector3.one * k * 0.9f, Vector3.one * k,
                color, Gfx.WithAlpha(color, 0f), duration, Gfx.OrderZone - 3);
        }
    }

    /// <summary>
    /// 파티클 이펙트. 파티클 시스템 3개(가산 빛 입자 / 속도 방향으로 늘어나는 불꽃 줄기 / 연기)를 공유하고
    /// 필요한 곳에서 Emit만 호출한다. 프레임당 방출 수에 상한이 있어 적이 많아도 부담이 일정하다.
    /// </summary>
    public static class Vfx
    {
        const int MaxEmitPerFrame = 260;
        const int MaxHitsPerFrame = 10;

        static bool materialsReady;
        static Material additive, flash, glowMat, smokeMat;
        static Transform root;
        static ParticleSystem glow, streak, smoke;
        static ParticleSystem.EmitParams ep;
        static int frame, emitted, hits;

        /// <summary>가산 혼합 스프라이트 머티리얼 (셰이더가 없으면 일반 머티리얼)</summary>
        public static Material Additive { get { EnsureMaterials(); return additive; } }
        /// <summary>피격 번쩍임 머티리얼 (셰이더가 없으면 null)</summary>
        public static Material Flash { get { EnsureMaterials(); return flash; } }

        static void EnsureMaterials()
        {
            if (materialsReady && additive != null) return;
            GameDatabase db = GameManager.Db;
            if (db == null) return;
            materialsReady = true;
            Shader fx = Resources.Load<Shader>("Shaders/EMFx");
            Shader fl = Resources.Load<Shader>("Shaders/EMFlash");
            bool fxOk = fx != null && fx.isSupported;
            Texture tex = db.softCircle != null ? db.softCircle.texture : null;

            additive = fxOk ? new Material(fx) : db.spriteMaterial;
            glowMat = fxOk ? new Material(fx) : new Material(db.spriteMaterial);
            smokeMat = fxOk ? new Material(fx) : new Material(db.spriteMaterial);
            if (fxOk)
            {
                additive.SetFloat("_DstBlend", 1f);
                glowMat.SetFloat("_DstBlend", 1f);
                smokeMat.SetFloat("_DstBlend", 10f);
                // 파티클은 정점 색을 쓰고, 스프라이트는 렌더러 색(unity_SpriteColor)을 쓴다
                additive.SetFloat("_UseSpriteColor", 1f);
                glowMat.SetFloat("_UseSpriteColor", 0f);
                smokeMat.SetFloat("_UseSpriteColor", 0f);
            }
            else
            {
                Debug.LogWarning("Vfx: 이펙트 셰이더(Resources/Shaders/EMFx)를 쓸 수 없어 일반 머티리얼로 대체합니다.");
            }
            glowMat.mainTexture = tex;
            smokeMat.mainTexture = tex;
            flash = fl != null && fl.isSupported ? new Material(fl) : null;
        }

        static bool Ready()
        {
            if (root != null) return true;
            if (GameManager.Db == null) return false;
            EnsureMaterials();
            root = new GameObject("Vfx").transform;
            glow = Make("Glow", glowMat, Gfx.OrderFx + 2, false, false, 2.5f);
            streak = Make("Streak", glowMat, Gfx.OrderFx + 3, true, false, 4f);
            smoke = Make("Smoke", smokeMat, Gfx.OrderFx - 3, false, true, 2f);
            return true;
        }

        static ParticleSystem Make(string name, Material mat, int order, bool stretch, bool grow, float drag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                grow
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = grow
                ? new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.5f))
                : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.dampen = 0f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.sortingOrder = order;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.07f;
                renderer.lengthScale = 1.4f;
            }
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector2 pos, Vector2 vel, float size, float life, Color color)
        {
            if (Time.frameCount != frame) { frame = Time.frameCount; emitted = 0; hits = 0; }
            if (emitted >= MaxEmitPerFrame) return;
            emitted++;
            ep.position = pos;
            ep.velocity = vel;
            ep.startSize = size;
            ep.startLifetime = life;
            ep.startColor = color;
            ps.Emit(ep, 1);
        }

        /// <summary>빛 입자 하나</summary>
        public static void Glow(Vector2 pos, Vector2 vel, float size, float life, Color color)
        {
            if (Ready()) Emit(glow, pos, vel, size, life, color);
        }

        /// <summary>속도 방향으로 늘어나는 불꽃 줄기 하나</summary>
        public static void Streak(Vector2 pos, Vector2 vel, float size, float life, Color color)
        {
            if (Ready()) Emit(streak, pos, vel, size, life, color);
        }

        /// <summary>연기 입자 하나 (일반 알파 혼합, 커지면서 사라진다)</summary>
        public static void Smoke(Vector2 pos, Vector2 vel, float size, float life, Color color)
        {
            if (Ready()) Emit(smoke, pos, vel, size, life, color);
        }

        /// <summary>사방으로 튀는 빛 입자</summary>
        public static void Burst(Vector2 pos, Color color, int count, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float lifeMin, float lifeMax)
        {
            if (!Ready()) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
                Emit(glow, pos + dir * 0.1f, dir * Random.Range(speedMin, speedMax),
                    Random.Range(sizeMin, sizeMax), Random.Range(lifeMin, lifeMax), color);
            }
        }

        /// <summary>사방으로 튀는 불꽃 줄기</summary>
        public static void Sparks(Vector2 pos, Color color, int count, float speedMin, float speedMax)
        {
            if (!Ready()) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
                Emit(streak, pos, dir * Random.Range(speedMin, speedMax), Random.Range(0.07f, 0.13f),
                    Random.Range(0.16f, 0.32f), color);
            }
        }

        /// <summary>한쪽 방향으로 쏠려 튀는 불꽃 줄기</summary>
        public static void SparksCone(Vector2 pos, Vector2 dir, float spreadDeg, Color color, int count, float speedMin, float speedMax)
        {
            if (!Ready()) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Gfx.Rotate(dir, Random.Range(-spreadDeg, spreadDeg));
                Emit(streak, pos, d * Random.Range(speedMin, speedMax), Random.Range(0.07f, 0.14f),
                    Random.Range(0.16f, 0.34f), color);
            }
        }

        public static void SmokePuff(Vector2 pos, Color color, int count, float spread, float size, float life)
        {
            if (!Ready()) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread;
                Emit(smoke, pos + offset, offset * 0.8f + Vector2.up * Random.Range(0.2f, 0.7f),
                    size * Random.Range(0.7f, 1.2f), life * Random.Range(0.75f, 1.25f), color);
            }
        }

        /// <summary>피격 불꽃. 프레임당 횟수 상한이 있어 광역기가 수십 마리를 쳐도 일정하다.</summary>
        public static void Hit(Vector2 pos, Element element)
        {
            if (!Ready()) return;
            if (Time.frameCount != frame) { frame = Time.frameCount; emitted = 0; hits = 0; }
            if (hits >= MaxHitsPerFrame) return;
            hits++;
            Color c = element == Element.Fire ? new Color(1f, 0.72f, 0.28f)
                : element == Element.Water ? new Color(0.55f, 0.9f, 1f) : Color.white;
            Emit(glow, pos, Vector2.zero, 0.75f, 0.12f, Gfx.WithAlpha(c, 0.75f));
            for (int i = 0; i < 3; i++)
            {
                Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
                Emit(streak, pos, dir * Random.Range(4f, 8.5f), Random.Range(0.06f, 0.1f), Random.Range(0.12f, 0.22f), c);
            }
        }

        /// <summary>적 처치 연출: 흰 섬광 + 사방으로 터지는 조각</summary>
        public static void Death(Vector2 pos, float width)
        {
            if (!Ready()) return;
            Emit(glow, pos, Vector2.zero, width * 1.9f, 0.16f, new Color(1f, 1f, 1f, 0.7f));
            int n = Mathf.Clamp(Mathf.RoundToInt(width * 5f), 4, 8);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
                Emit(glow, pos + dir * (width * 0.2f), dir * Random.Range(2.2f, 5.5f), Random.Range(0.1f, 0.2f),
                    Random.Range(0.25f, 0.45f), new Color(1f, 0.96f, 0.85f, 0.9f));
            }
            Emit(smoke, pos, Vector2.up * 0.4f, width * 1.3f, 0.45f, new Color(0.85f, 0.87f, 0.95f, 0.3f));
        }

        /// <summary>물 튀김: 물방울이 흩어지고 작은 물결 고리가 퍼진다</summary>
        public static void Splash(Vector2 pos, Color color, float scale = 1f)
        {
            if (!Ready()) return;
            Color bright = Color.Lerp(color, Color.white, 0.45f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 dir = Gfx.Dir(Random.Range(0f, 360f));
                Emit(glow, pos, dir * Random.Range(1.6f, 4.2f) * scale, Random.Range(0.09f, 0.17f) * scale,
                    Random.Range(0.22f, 0.4f), bright);
            }
            Fx.Shockwave(pos, 0.55f * scale, Gfx.WithAlpha(bright, 0.6f), 0.26f);
        }
    }

    /// <summary>
    /// 타격감 연출: 화면 흔들림과 히트스톱(아주 짧은 시간 정지).
    /// CameraFollow가 매 프레임 Tick을 호출한다.
    /// </summary>
    public static class Juice
    {
        const float MaxOffset = 0.34f;      // 흔들림 최대 폭 (월드 단위)
        const float StopScale = 0.05f;
        const float StopGap = 0.3f;         // 히트스톱 사이의 최소 간격

        /// <summary>테스트·접근성용: 끄면 흔들림과 히트스톱이 모두 꺼진다</summary>
        public static bool Enabled = true;

        static float trauma, stopTimer, nextStopTime, savedScale = 1f;
        static bool stopping;

        public static void Reset()
        {
            trauma = 0f;
            stopTimer = 0f;
            nextStopTime = 0f;
            stopping = false;
        }

        /// <summary>흔들림을 더한다 (0~1 누적, 실제 폭은 제곱에 비례해서 작은 값은 거의 안 보인다)</summary>
        public static void Shake(float amount)
        {
            if (Enabled) trauma = Mathf.Min(1f, trauma + amount);
        }

        /// <summary>큰 타격 순간 아주 잠깐 시간을 늦춘다</summary>
        public static void HitStop(float seconds)
        {
            GameManager gm = GameManager.I;
            if (!Enabled || gm == null || gm.State != GameState.Playing) return;
            if (!stopping)
            {
                if (Time.unscaledTime < nextStopTime || Time.timeScale <= 0f) return;
                savedScale = Time.timeScale;
                stopping = true;
                Time.timeScale = savedScale * StopScale;
            }
            stopTimer = Mathf.Max(stopTimer, Mathf.Min(seconds, 0.15f));
        }

        /// <returns>이번 프레임의 카메라 흔들림 오프셋</returns>
        public static Vector2 Tick(float unscaledDt)
        {
            if (stopping)
            {
                GameManager gm = GameManager.I;
                if (gm == null || gm.State != GameState.Playing)
                {
                    // 레벨업·결과 화면이 시간을 직접 관리하므로 손대지 않고 물러난다
                    stopping = false;
                    stopTimer = 0f;
                }
                else
                {
                    stopTimer -= unscaledDt;
                    if (stopTimer <= 0f)
                    {
                        stopping = false;
                        Time.timeScale = savedScale;
                        nextStopTime = Time.unscaledTime + StopGap;
                    }
                }
            }

            trauma = Mathf.Max(0f, trauma - unscaledDt * 2.4f);
            if (trauma <= 0f) return Vector2.zero;
            float s = trauma * trauma * MaxOffset;
            float t = Time.unscaledTime * 42f;
            return new Vector2((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * s);
        }
    }
}
