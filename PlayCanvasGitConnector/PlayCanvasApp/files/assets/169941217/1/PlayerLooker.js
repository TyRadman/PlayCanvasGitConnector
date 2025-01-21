var PlayerLooker = pc.createScript('PlayerLooker');

PlayerLooker.attributes.add('LookDistance', {
    type: 'number',    
    default: 5,
});

PlayerLooker.attributes.add('PlayerEntity', {
    type: 'entity',
    description: 'Reference to the player'
});

PlayerLooker.attributes.add('TargetEntity', {
    type: 'entity',
    description: 'Target Entity'
});

PlayerLooker.attributes.add('defaultImageEntity', { type: 'entity', title: 'Default Image Entity' });
PlayerLooker.attributes.add('interactionImageEntity', { type: 'entity', title: 'Interaction Image Entity' });
PlayerLooker.attributes.add('interactionText', { type: 'entity', title: 'Interaction Text' });
PlayerLooker.attributes.add('controlsGuide', { type: 'entity', title: 'Controls Guide' });

let isInteractable = false;
let isInteracting = false;


// initialize code called once per entity
PlayerLooker.prototype.initialize = function() 
{
    this.app.mouse.on(pc.EVENT_MOUSEDOWN, this.onMouseClicked, this);
    this.defaultImageEntity.enabled = true;
    this.interactionImageEntity.enabled = false;
};

PlayerLooker.prototype.update = function(dt) 
{
    if( isInteracting)
    {
        return;
    }

    if(this.app.keyboard.isPressed(pc.KEY_ESCAPE))
    {
        this.EnableCursor(true);
    }

    var screenCenter = new pc.Vec2(this.app.graphicsDevice.width / 2, this.app.graphicsDevice.height / 2);
    var from = this.entity.camera.screenToWorld(screenCenter.x, screenCenter.y, this.entity.camera.nearClip);
    var to = this.entity.camera.screenToWorld(screenCenter.x, screenCenter.y, 10);


    // Perform the raycast
    var result = this.app.systems.rigidbody.raycastFirst(from, to, this.DetectionLayer);

    var distance = 0;

    if(result)
    {
        distance = from.distance(result.point);
    }

    if(result && result.entity.script && result.entity.script.interactable)
    {
        // if the newly detected target isn't the same as the previous one, then disable the previos one's text
        if(this.TargetEntity && this.TargetEntity != result.entity && this.TargetEntity.script.interactable.DetailsText)
        {
            this.TargetEntity.script.interactable.DetailsText.enabled = false;
        }

        this.TargetEntity = result.entity;

        if(this.TargetEntity.script.interactable.DetailsText)
        {
            this.TargetEntity.script.interactable.DetailsText.enabled = true;
        }
 
        this.EnableInteractionCrosshair(true);
        this.controlsGuide.script.controlGuidesController.enableLeftClick(true);
    }
    else
    {
        if(this.TargetEntity && this.TargetEntity.script.interactable.DetailsText)
        {
            this.TargetEntity.script.interactable.DetailsText.enabled = false;
        }

        this.TargetEntity = null;
        this.EnableInteractionCrosshair(false);
        this.controlsGuide.script.controlGuidesController.enableLeftClick(false);
        return;
    }
};

PlayerLooker.prototype.EnableInteractionCrosshair = function(enable)
{
        // if(enable == isInteractable)
        // {
        //     return;
        // }

        // isInteractable = enable;

        if(this.defaultImageEntity)
        {
            this.defaultImageEntity.enabled = !enable;
        }

        if(this.interactionImageEntity)
        {
            this.interactionImageEntity.enabled = enable;
        }
        
        // set the interaction text
        if(enable && this.TargetEntity != null)
        {
            var textElement = this.interactionText.element;
            textElement.text = this.TargetEntity.script.interactable.interactionMessage;
        }
        else
        {
            this.interactionText.element.text = "";
        }
}

PlayerLooker.prototype.onMouseClicked = function(e){

    if (e.button === pc.MOUSEBUTTON_LEFT) 
    {
        this.onLeftMouseDown(e);
    } 
    else if (e.button === pc.MOUSEBUTTON_RIGHT)
    {
        this.onRightMouseDown(e);
    }
}

PlayerLooker.prototype.onLeftMouseDown = function(e) 
{
    if (this.TargetEntity && !isInteracting) 
    {
        // if the interactable is not an external link, then trigger the portfolio HTML and js script
        if(!this.TargetEntity.script.interactable.isExternalLink)
        {
            let panelName = this.TargetEntity.script.interactable.panelName;
            console.log('opened');
            window.parent.postMessage({ panelName: panelName, message: 'open'}, "*");
        }
        else
        {
            let externalLinkURL = this.TargetEntity.script.interactable.panelName;
            window.open(externalLinkURL, '_blank');
        }

        // disable the player's movement when the info panel is opened
        this.EnablePlayerMovement(false);
        this.EnableCursor(true);
        isInteracting = true;
        this.controlsGuide.script.controlGuidesController.enableLeftClick(false);
        this.controlsGuide.script.controlGuidesController.enableMovementGuides(false);
        this.controlsGuide.script.controlGuidesController.enableRightClick(true);
    }
    else if(isInteracting)
    {
        this.CloseProjectPage();
        // this.EnableCursor(true);
    }
};

PlayerLooker.prototype.onRightMouseDown = function(e) 
{
        this.CloseProjectPage();
};

PlayerLooker.prototype.CloseProjectPage = function()
{
    if(!this.TargetEntity || !isInteracting)
    {
        return;
    }

    window.parent.postMessage({ panelName:  this.TargetEntity.script.interactable.panelName, message: 'close'}, "*");
    this.EnablePlayerMovement(true);
    this.EnableCursor(false);
    this.TargetEntity = null;
    isInteracting = false;
    // disable the right click guide
    this.controlsGuide.script.controlGuidesController.enableRightClick(false);
    this.controlsGuide.script.controlGuidesController.enableMovementGuides(true);
}

PlayerLooker.prototype.EnablePlayerMovement = function(enable){
    if(this.PlayerEntity)
    {
        let playerMovement = this.PlayerEntity.script.firstPersonController;
        playerMovement.IsActive = enable;
    }
    else
    {
        console.log('no player reference');
    }
}

PlayerLooker.prototype.EnableCursor = function(enable){
    
    if (enable) {
        // Show the mouse cursor
        document.body.style.cursor = 'default'; // Make cursor visible
            document.exitPointerLock(); // Exit pointer lock if it's active
    } else {
        // Hide the mouse cursor
        document.body.style.cursor = 'none'; // Hide cursor
        this.app.mouse.enablePointerLock(); // Optionally lock the pointer for gameplay
    }
}