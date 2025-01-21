var FirstPersonController = pc.createScript('firstPersonController');

FirstPersonController.attributes.add('camera', {
    type: 'entity',description: 'Optional, assign a camera entity, otherise one is created'
});

FirstPersonController.attributes.add('head', {
    type: 'entity',description: 'The camera parent'
});

FirstPersonController.attributes.add('walkingSpeed', {
    type: 'number',default: 10,description: 'Adjusts the speed of player movement'
});

FirstPersonController.attributes.add('lookSpeed', {
    type: 'number',default: 0.25,description: 'Adjusts the sensitivity of looking'
});

FirstPersonController.attributes.add('lookLimit', {
    type: 'number',default: 80,description: 'The max angle at which the player can look'
});

FirstPersonController.attributes.add('shiftFactor', {
    type: 'number',default: 3,description: 'Speed factor when shift is pressed'
});

FirstPersonController.attributes.add('IsActive', {type: 'boolean', default: true});


// initialize code called once per entity
FirstPersonController.prototype.initialize = function() {
    // this.app.mouse.enablePointerLock();

    this.force = new pc.Vec3();
    this.eulers = new pc.Vec3();
    var app = this.app;
    
    // listen for mouse move events
    app.mouse.on("mousemove", this._onMouseMove, this);
    app.mouse.enablePointerLock();

    // when the mouse is clicked hide the cursor
    app.mouse.on("mousedown", function() {
         app.mouse.enablePointerLock();
    }, this);
    
    // check for required components
    if(!this.entity.collision) {
        console.error("Expected 'collision' component.");
    }
    
    if(!this.entity.rigidbody || this.entity.rigidbody.type !== pc.BODYTYPE_DYNAMIC) {
        console.error("Expected DYNAMIC 'rigidbody' component");
    }
    
};

// update code called every frame
FirstPersonController.prototype.update = function(dt) {

    if (!this.IsActive) 
    {
        return;
    }
    
    var force = this.force;
    var app = this.app;
    
    // get camara directions to determin movements directions
    var forward = this.camera.forward;
    var right = this.camera.right;
    
    // movement
    var x = 0;
    var z = 0;
    
    // check for key presses
    if(app.keyboard.isPressed(pc.KEY_A)) {
        x -= right.x;
        z -= right.z;
    }
    
    if(app.keyboard.isPressed(pc.KEY_D)) {
        x += right.x;
        z += right.z;
    }
    
    if(app.keyboard.isPressed(pc.KEY_W)) {
        x += forward.x;
        z += forward.z;
    }

    if(app.keyboard.isPressed(pc.KEY_S)) {
        x -= forward.x;
        z -= forward.z;
    }
    
    // use direction from keypresses to apply a force to the character
    if(x !== 0 && z !== 0) 
    {
        force.set(x, 0, z).normalize().scale(this.walkingSpeed);
        var finalForce = force.scale(app.keyboard.isPressed(pc.KEY_SHIFT) ? this.shiftFactor : 1);
        this.entity.rigidbody.applyForce(finalForce);
    }
    
    // update camera angle from mouse events
    this.camera.setLocalEulerAngles(this.eulers.y, this.eulers.x, 0);
    // this.camera.setLocalEulerAngles(this.eulers.y, 0, 0);
    // this.head.setLocalEulerAngles(0, this.eulers.x, 0);
    // console.log(this.head.getLocalEulerAngles(), this.eulers);
   
};


FirstPersonController.prototype._onMouseMove = function(e) {
   if (!this.IsActive)
    {
        return;
    }
    // Define maximum allowable deltas
    var maxDelta = 50;
    // Clamp the deltas to prevent excessive movement
    var clampedDx = pc.math.clamp(e.dx, -maxDelta, maxDelta);
    var clampedDy = pc.math.clamp(e.dy, -maxDelta, maxDelta);

    if (pc.Mouse.isPointerLocked()) {
        // Adjust horizontal rotation
        this.eulers.x -= this.lookSpeed * clampedDx;
        this.eulers.y -= this.lookSpeed * clampedDy;

        // Adjust vertical rotation and clamp it within the defined limits
        this.eulers.y = pc.math.clamp(this.eulers.y, -this.lookLimit, this.lookLimit);
    }

   return;
};

FirstPersonController.prototype.ResetPlayer = function(){
    console.log('camera reset');
    this.camera.setLocalEulerAngles(0, 0, 0);
    this.entity.setLocalEulerAngles(0, 0, 0);
};