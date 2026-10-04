// ---------- two-bone IK (show poses: squat feet / hands on knees / hand in pocket / prop at chest) ----------
const _ik = { a: new THREE.Vector3(), t: new THREE.Vector3(), p: new THREE.Vector3(), d: new THREE.Vector3(), b: new THREE.Vector3(), u: new THREE.Vector3(), e: new THREE.Vector3(), x: new THREE.Vector3(), y: new THREE.Vector3(), z: new THREE.Vector3(), m: new THREE.Matrix4(), qp: new THREE.Quaternion(), qw: new THREE.Quaternion(), qu: new THREE.Quaternion(), ql: new THREE.Quaternion(), q0: new THREE.Quaternion(), q1: new THREE.Quaternion() };

// upper/lower: joint groups whose bones extend along local -Y; lower sits at (0,-L1,0) inside upper.
// Puts the end of `lower` (its local (0,-L2,0)) on targetWorld, bending toward poleWorld.
// The bend is a pure rotation of `lower` about its local X: sign +1 = legs (shin folds back), -1 = arms (forearm folds forward).
function solveTwoBone(upper, lower, L1, L2, targetWorld, poleWorld, weight = 1, sign = 1) {
  const k = _ik;
  if (weight <= 0) return;
  upper.parent.updateWorldMatrix(true, false);
  upper.parent.getWorldQuaternion(k.qp);
  k.a.setFromMatrixPosition(k.m.multiplyMatrices(upper.parent.matrixWorld, k.m.compose(upper.position, upper.quaternion, upper.scale)));
  k.d.subVectors(targetWorld, k.a);
  let dist = k.d.length();
  const maxD = (L1 + L2) * .99999, minD = Math.abs(L1 - L2) + 1e-5;
  if (dist < 1e-6) { k.d.set(0, -1, 0); dist = minD; } else k.d.multiplyScalar(1 / dist);
  dist = clamp(dist, minD, maxD);
  // bend direction: pole projected perpendicular to the a→t line
  k.b.subVectors(poleWorld, k.a); k.b.addScaledVector(k.d, -k.b.dot(k.d));
  if (k.b.lengthSq() < 1e-10) { k.b.set(0, 0, 1).addScaledVector(k.d, -k.d.z); if (k.b.lengthSq() < 1e-10) k.b.set(1, 0, 0).addScaledVector(k.d, -k.d.x); }
  k.b.normalize();
  const cosA = clamp((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist), -1, 1), sinA = Math.sqrt(1 - cosA * cosA);
  // upper bone direction u (from joint toward elbow/knee)
  k.u.copy(k.d).multiplyScalar(cosA).addScaledVector(k.b, sinA).normalize();
  // upper frame: -Y = u ; +Z = bend side (legs) or opposite (arms) ; X = Y × Z
  k.y.copy(k.u).negate();
  // u rotated 90° toward the pole inside the bend plane (stays continuous when the shoulder angle passes 90°)
  k.z.copy(k.d).multiplyScalar(-sinA).addScaledVector(k.b, cosA).normalize().multiplyScalar(sign >= 0 ? 1 : -1);
  k.x.crossVectors(k.y, k.z).normalize();
  k.m.makeBasis(k.x, k.y, k.z);
  k.qw.setFromRotationMatrix(k.m);
  k.qu.copy(k.qp).invert().multiply(k.qw);
  // interior angle at the middle joint
  const cosB = clamp((L1 * L1 + L2 * L2 - dist * dist) / (2 * L1 * L2), -1, 1), bend = Math.PI - Math.acos(cosB);
  k.ql.setFromAxisAngle(k.x.set(1, 0, 0), sign >= 0 ? bend : -bend);
  if (weight >= 1) { upper.quaternion.copy(k.qu); lower.quaternion.copy(k.ql); }
  else { upper.quaternion.slerp(k.qu, weight); lower.quaternion.slerp(k.ql, weight); }
}

// ikTargets (root-local, m): { L:{hand,pole,hide?}, R:{...}, FL:{foot,pole,flat,yaw}, FR:{...} }
Fighter.prototype.applyIK = function (w, busy) {
  const T = this.ikTargets, J = this.J;
  const restoreHands = () => { for (const s of ['L', 'R']) if (J['ha' + s] && J['ha' + s].userData.ikHidden) { J['ha' + s].visible = true; J['ha' + s].userData.ikHidden = false; } };
  if (!T || busy || w <= .001) { restoreHands(); return; }
  w = Math.min(1, w);
  this.root.updateMatrixWorld(true);
  const k = _ik, tw = new THREE.Vector3(), pw = new THREE.Vector3();
  for (const s of ['FL', 'FR']) {
    const t = T[s]; if (!t) continue;
    const side = s[1], th = J['th' + side], sh = J['sh' + side], ft = J['ft' + side];
    tw.set(...t.foot); this.root.localToWorld(tw); pw.set(...t.pole); this.root.localToWorld(pw);
    solveTwoBone(th, sh, this.thighL, this.shinL, tw, pw, w, 1);
    if (t.flat) {
      // foot parallel to the ground: world orientation = root yaw (+ optional extra yaw)
      th.updateWorldMatrix(true, true);
      sh.getWorldQuaternion(k.q0);
      this.root.getWorldQuaternion(k.q1);
      if (t.yaw) k.q1.multiply(k.qw.setFromAxisAngle(k.x.set(0, 1, 0), t.yaw));
      k.q0.invert().multiply(k.q1);
      ft.quaternion.slerp(k.q0, w);
    }
  }
  for (const s of ['L', 'R']) {
    const t = T[s], ua = J['ua' + s], fa = J['fa' + s], ha = J['ha' + s];
    if (!t) continue;
    tw.set(...t.hand); this.root.localToWorld(tw); pw.set(...t.pole); this.root.localToWorld(pw);
    solveTwoBone(ua, fa, this.upperL, this.foreL, tw, pw, w, -1);
    if (t.hide && w > .5) { ha.visible = false; ha.userData.ikHidden = true; }
    else if (ha.userData.ikHidden) { ha.visible = true; ha.userData.ikHidden = false; }
  }
};
