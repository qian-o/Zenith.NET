// A slowly rotating Canvas geometry study, rendered only while visible.
export function initializeHomeScene() {
    const canvas = document.getElementById('home-geometry');
    if (!canvas) return;
    const context = canvas.getContext('2d');
    if (!context) return;
    const stage = canvas.parentElement;
    const modeButtons = [...stage.querySelectorAll('[data-scene-mode]')];
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
    const faces = [];
    const steps = 144;
    const sides = 12;
    const rotationSpeed = .16;
    const rotationAcceleration = .2;
    const pointerSensitivity = .008;
    const keyboardStep = .15;
    const keyboardPauseMs = 1000;
    const maxFrameSeconds = .05;
    const maxPixelRatio = 2;
    let yaw = .4;
    let pitch = -.45;
    let mode = 'surface';
    let pointer;
    let visible = false;
    let suspended = false;
    let frame = 0;
    let lastTime = 0;
    let speed = 0;
    let pauseUntil = 0;
    let dirty = true;
    let width = 0, height = 0, ratio = 1;

    const normalize = vector => {
        const length = Math.hypot(...vector);
        return vector.map(value => value / length);
    };
    const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const lightDirection = normalize([-.55, -.8, 1]);
    const halfDirection = normalize([lightDirection[0], lightDirection[1], lightDirection[2] + 1]);
    const dot = (a, b) => a.reduce((sum, value, i) => sum + value * b[i], 0);
    const center = t => [(1.2 + .38 * Math.cos(3 * t)) * Math.cos(2 * t), (1.2 + .38 * Math.cos(3 * t)) * Math.sin(2 * t), .58 * Math.sin(3 * t)];
    const point = (step, side) => {
        const t = step / steps * Math.PI * 2;
        const c = center(t);
        const next = center(t + .001);
        const tangent = normalize(next.map((v, i) => v - c[i]));
        const normal = normalize(cross(tangent, [0, 0, 1]));
        const binormal = cross(tangent, normal);
        const angle = side / sides * Math.PI * 2;
        return c.map((v, i) => v + .26 * (normal[i] * Math.cos(angle) + binormal[i] * Math.sin(angle)));
    };
    for (let i = 0; i < steps; i++) {
        for (let j = 0; j < sides; j++) {
            const vertices = [point(i, j), point(i + 1, j), point(i + 1, j + 1), point(i, j + 1)];
            const normal = normalize(cross(vertices[3].map((v, index) => v - vertices[0][index]), vertices[1].map((v, index) => v - vertices[0][index])));
            faces.push({ vertices, normal });
        }
    }

    function draw() {
        if (!width || !height) return;
        context.setTransform(ratio, 0, 0, ratio, 0, 0);
        context.clearRect(0, 0, width, height);
        const cy = Math.cos(yaw), sy = Math.sin(yaw), cp = Math.cos(pitch), sp = Math.sin(pitch);
        const rotate = ([x, y, z]) => {
            const a = x * cy + z * sy, b = z * cy - x * sy;
            return [a, y * cp - b * sp, y * sp + b * cp];
        };
        const scale = Math.min(width, height) * .24;
        const project = ([x, y, z]) => {
            const perspective = 5 / (5 - z);
            return [width / 2 + x * scale * perspective, height / 2 + y * scale * perspective];
        };
        const transformed = faces.map(face => {
            const vertices = face.vertices.map(rotate);
            return { vertices, normal: rotate(face.normal), depth: vertices.reduce((sum, v) => sum + v[2], 0) / 4 };
        }).sort((a, b) => a.depth - b.depth);
        for (const { vertices, normal, depth } of transformed) {
            const diffuse = Math.max(0, dot(normal, lightDirection));
            const highlight = Math.pow(Math.max(0, dot(normal, halfDirection)), 28);
            const brightness = Math.round(18 + diffuse * 135 + highlight * 70 + (depth + 2) * 5);
            const points = vertices.map(project);
            context.beginPath();
            points.forEach(([x, y], i) => i ? context.lineTo(x, y) : context.moveTo(x, y));
            context.closePath();
            if (mode === 'surface') {
                context.fillStyle = `rgb(${brightness}, ${brightness + 4}, ${brightness + 10})`;
                context.fill();
                context.strokeStyle = 'rgba(220,230,244,.07)';
                context.lineWidth = .5;
            } else {
                context.strokeStyle = `rgba(200,211,226,${.1 + (depth + 1.7) * .12})`;
                context.lineWidth = .6;
            }
            context.stroke();
        }
    }

    function schedule() {
        if (!frame && visible && !document.hidden && !suspended) frame = requestAnimationFrame(tick);
    }

    function tick(time) {
        frame = 0;
        const elapsed = lastTime ? Math.min((time - lastTime) / 1000, maxFrameSeconds) : 0;
        lastTime = time;
        const rotating = !preference.matches && !pointer && time >= pauseUntil;
        if (rotating) {
            // Ease back into a roughly 40-second revolution after manual input.
            speed = Math.min(rotationSpeed, speed + elapsed * rotationAcceleration);
            yaw = (yaw - speed * elapsed) % (Math.PI * 2);
        }
        if (dirty || rotating) draw();
        dirty = false;
        if (!preference.matches && !pointer) schedule();
    }

    function syncMotion() {
        cancelAnimationFrame(frame);
        frame = 0;
        lastTime = 0;
        speed = 0;
        if (dirty || !preference.matches && !pointer) schedule();
    }

    function render() {
        dirty = true;
        schedule();
    }

    preference.addEventListener('change', syncMotion);

    modeButtons.forEach(button => {
        button.addEventListener('click', () => {
            mode = button.dataset.sceneMode;
            modeButtons.forEach(item => item.setAttribute('aria-pressed', String(item === button)));
            render();
        });
    });
    canvas.addEventListener('pointerdown', event => {
        if (event.button !== 0 || pointer) return;
        pointer = { id: event.pointerId, x: event.clientX, y: event.clientY };
        canvas.setPointerCapture(event.pointerId);
        canvas.classList.add('is-dragging');
        syncMotion();
    });
    canvas.addEventListener('pointermove', event => {
        if (!pointer || pointer.id !== event.pointerId) return;
        yaw += (event.clientX - pointer.x) * pointerSensitivity;
        pitch += (event.clientY - pointer.y) * pointerSensitivity;
        pointer.x = event.clientX;
        pointer.y = event.clientY;
        render();
    });
    const release = () => {
        if (!pointer) return;
        pointer = undefined;
        canvas.classList.remove('is-dragging');
        syncMotion();
    };
    canvas.addEventListener('pointerup', release);
    canvas.addEventListener('pointercancel', release);
    canvas.addEventListener('lostpointercapture', release);
    canvas.addEventListener('keydown', event => {
        const delta = { ArrowLeft: [-keyboardStep, 0], ArrowRight: [keyboardStep, 0], ArrowUp: [0, -keyboardStep], ArrowDown: [0, keyboardStep] }[event.key];
        if (!delta) return;
        event.preventDefault();
        yaw += delta[0];
        pitch += delta[1];
        pauseUntil = performance.now() + keyboardPauseMs;
        speed = 0;
        render();
    });
    new ResizeObserver(() => {
        ({ width, height } = canvas.getBoundingClientRect());
        ratio = Math.min(devicePixelRatio || 1, maxPixelRatio);
        const pixelWidth = Math.round(width * ratio), pixelHeight = Math.round(height * ratio);
        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }
        render();
    }).observe(canvas);
    new IntersectionObserver(([entry]) => {
        visible = entry.isIntersecting;
        syncMotion();
    }).observe(canvas);
    document.addEventListener('visibilitychange', syncMotion);
    window.addEventListener('pagehide', () => {
        suspended = true;
        release();
        syncMotion();
    });
    window.addEventListener('pageshow', () => {
        suspended = false;
        dirty = true;
        syncMotion();
    });
    syncMotion();
}
